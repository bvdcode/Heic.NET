// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using System;
using System.IO;
using Heic.Net.Hevc;
using Xunit;

namespace Heic.Net.Tests
{
    public class ResidualLevelTests
    {
        [Theory]
        [InlineData(0)]
        [InlineData(25)]
        [InlineData(51)]
        public void GroupsPreserveMagnitudeEscapesSignsAndTermination(int qp)
        {
            int[] diagonal = { 0, 4, 1, 8, 5, 2, 12, 9, 6, 3, 13, 10, 7, 14, 11, 15 };
            int[] boundaries = { 1, 2, 3, 4, 7, 8, 15, 16, 31, 32, 63, 64, 255, 256, 4095, 32767 };
            for (int count = 1; count <= 16; count++)
            {
                for (int pattern = 0; pattern < 48; pattern++)
                {
                    int[] magnitudes = new int[count];
                    int sum = 0;
                    for (int i = 0; i < count; i++)
                    {
                        int magnitude = pattern switch
                        {
                            < 16 => boundaries[pattern],
                            < 32 => i == (pattern - 16) ? 3 : 1,
                            _ => boundaries[(i + pattern) % boundaries.Length]
                        };
                        magnitudes[i] = magnitude;
                        sum += magnitude;
                    }
                    foreach (bool signHiding in new[] { false, true })
                    {
                        bool hidden = signHiding && (count > 4);
                        int negativeSigns = (pattern & 1) == 0 ? 0xaaaa : 0x5555;
                        byte[] encoded = ResidualGroupWriter.Write(magnitudes, negativeSigns, hidden, qp);
                        IntraCabacReader reader = new IntraCabacReader(new BitReader(encoded), qp);
                        int[] output = new int[17];
                        Array.Fill(output, 999);
                        Assert.False(new ResidualDecoder(reader, false, signHiding).Read(output, 2, 0, 0, false));
                        for (int i = 0; i < 16; i++)
                        {
                            int expected = 0;
                            if (i < count)
                            {
                                bool negative = hidden && (i == (count - 1))
                                    ? (sum & 1) != 0
                                    : (negativeSigns & (1 << i)) != 0;
                                expected = negative ? -magnitudes[i] : magnitudes[i];
                            }
                            Assert.Equal(expected, output[diagonal[15 - i]]);
                        }
                        Assert.Equal(999, output[16]);
                        Assert.True(reader.Termination());
                    }
                }
            }
        }

        [Fact]
        public void SignedPrecisionBoundaryPreservesNegativeMinimumAndRejectsOverflow()
        {
            foreach (int magnitude in new[] { 32767, 32768, 32769 })
            {
                foreach (bool negative in new[] { false, true })
                {
                    byte[] encoded = ResidualGroupWriter.Write(new[] { magnitude }, negative ? 1 : 0, false, 25);
                    IntraCabacReader reader = new IntraCabacReader(new BitReader(encoded), 25);
                    ResidualDecoder decoder = new ResidualDecoder(reader, false, false);
                    int[] output = new int[16];
                    if (magnitude > (negative ? 32768 : 32767))
                    {
                        Assert.Throws<InvalidDataException>(() => decoder.Read(output, 2, 0, 0, false));
                    }
                    else
                    {
                        Assert.False(decoder.Read(output, 2, 0, 0, false));
                        Assert.Equal(negative ? -magnitude : magnitude, output[15]);
                        Assert.True(reader.Termination());
                    }
                }
            }
        }
    }
}
