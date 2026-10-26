// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using System;
using Heic.Net.Hevc;
using Xunit;

namespace Heic.Net.Tests
{
    public class ResidualDecoderTests
    {
        [Theory]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        public void DecodesASingleDcCoefficientAndItsSign(bool negative, bool skip)
        {
            CabacIntervalWriter writer = new CabacIntervalWriter(25);
            writer.Decision(skip, 0, 139);
            writer.Decision(false, 1, 110);
            writer.Decision(false, 2, 110);
            writer.Decision(false, 3, 92);
            writer.Bypass(negative);
            writer.Terminate();
            IntraCabacReader reader = new IntraCabacReader(new BitReader(writer.ToArray()), 25);
            ResidualDecoder decoder = new ResidualDecoder(reader, true, true);
            int[] output = new int[16];
            Array.Fill(output, 999);
            Assert.Equal(skip, decoder.Read(output, 2, 0, 0, false));
            Assert.Equal(negative ? -1 : 1, output[0]);
            Assert.All(output.AsSpan(1).ToArray(), coefficient => Assert.Equal(0, coefficient));
            Assert.True(reader.Termination());
        }

        [Fact]
        public void ArithmeticReaderMatchesAnExactIntervalAcrossAdaptiveDecisions()
        {
            Random random = new Random(8346);
            CabacIntervalWriter writer = new CabacIntervalWriter(22);
            bool[] expected = new bool[500];
            for (int i = 0; i < expected.Length; i++)
            {
                expected[i] = random.Next(4) == 0;
                if ((i % 3) == 0)
                {
                    writer.Bypass(expected[i]);
                }
                else
                {
                    writer.Decision(expected[i], 1, 139);
                }
            }

            writer.Terminate();
            CabacDecoder decoder = new CabacDecoder(new BitReader(writer.ToArray()));
            CabacContext context = CabacContext.Initialize(139, 22);
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.Equal(expected[i], (i % 3) == 0 ? decoder.ReadBypass() : decoder.ReadBin(ref context));
            }

            Assert.True(decoder.ReadTermination());
        }

        [Theory]
        [InlineData(3)]
        [InlineData(7)]
        [InlineData(31)]
        [InlineData(32767)]
        public void ReadsRiceAndExponentialGolombCoefficientEscapes(int level)
        {
            CabacIntervalWriter writer = new CabacIntervalWriter(25);
            writer.Decision(false, 0, 110);
            writer.Decision(false, 1, 110);
            writer.Decision(true, 2, 92);
            writer.Decision(true, 3, 138);
            writer.Bypass(false);
            writer.RemainingLevel(level - 3, 0);
            writer.Terminate();
            IntraCabacReader reader = new IntraCabacReader(new BitReader(writer.ToArray()), 25);
            int[] output = new int[16];
            new ResidualDecoder(reader, false, true).Read(output, 2, 0, 0, false);
            Assert.Equal(level, output[0]);
            Assert.True(reader.Termination());
        }

        [Fact]
        public void HiddenSignUsesTheParityOfAllLevelsInTheGroup()
        {
            CabacIntervalWriter writer = new CabacIntervalWriter(25);
            byte[] prefixInitial = { 110, 110, 124 };
            for (int coordinate = 0; coordinate < 2; coordinate++)
            {
                for (int i = 0; i < 3; i++)
                {
                    writer.Decision(true, (coordinate * 10) + i, prefixInitial[i]);
                }
            }

            int[] diagonal = { 0, 4, 1, 8, 5, 2, 12, 9, 6, 3, 13, 10, 7, 14, 11, 15 };
            int[] map = { 0, 1, 4, 5, 2, 3, 4, 5, 6, 6, 8, 8, 7, 7, 8, 8 };
            byte[] significantInitial = { 111, 111, 125, 110, 110, 94, 124, 108, 124 };
            for (int n = 14; n >= 0; n--)
            {
                int context = map[diagonal[n]];
                writer.Decision(n == 0, 100 + context, significantInitial[context]);
            }

            writer.Decision(false, 200, 92);
            writer.Decision(true, 201, 137);
            writer.Decision(true, 300, 138);
            writer.Bypass(true);
            writer.RemainingLevel(1, 0);
            writer.Terminate();
            IntraCabacReader reader = new IntraCabacReader(new BitReader(writer.ToArray()), 25);
            int[] output = new int[16];
            new ResidualDecoder(reader, false, true).Read(output, 2, 0, 0, false);
            Assert.Equal(-1, output[15]);
            Assert.Equal(-4, output[0]);
            Assert.True(reader.Termination());
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(true, true)]
        public void GroupsPreserveEverySignPositionCountAndFollowingTermination(bool signHiding, bool bypass)
        {
            int[] diagonal = { 0, 4, 1, 8, 5, 2, 12, 9, 6, 3, 13, 10, 7, 14, 11, 15 };
            for (int count = 1; count <= 16; count++)
            {
                for (int pattern = 0; pattern < 19; pattern++)
                {
                    int signs = pattern switch
                    {
                        0 => 0,
                        1 => 65535,
                        2 => 0xaaaa,
                        _ => 1 << (pattern - 3)
                    };
                    bool hidden = signHiding && !bypass && (count > 4);
                    int signCount = count - (hidden ? 1 : 0);
                    int[] magnitudes = new int[count];
                    Array.Fill(magnitudes, 1);
                    byte[] encoded = ResidualGroupWriter.Write(magnitudes, signs, hidden, 25);
                    IntraCabacReader reader = new IntraCabacReader(new BitReader(encoded), 25);
                    int[] output = new int[17];
                    Array.Fill(output, 999);
                    ResidualDecoder decoder = new ResidualDecoder(reader, false, signHiding);
                    Assert.False(decoder.Read(output, 2, 0, 0, bypass));
                    for (int i = 0; i < 16; i++)
                    {
                        int expected = 0;
                        if (i < count)
                        {
                            bool negative = (i < signCount) && ((signs & (1 << i)) != 0);
                            if (hidden && (i == (count - 1)))
                            {
                                negative = (count & 1) != 0;
                            }
                            expected = negative ? -1 : 1;
                        }
                        Assert.Equal(expected, output[diagonal[15 - i]]);
                    }
                    Assert.Equal(999, output[16]);
                    Assert.True(reader.Termination());
                }
            }
        }
    }
}
