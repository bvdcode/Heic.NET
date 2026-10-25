// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Heic.Net.Hevc;
using Xunit;

namespace Heic.Net.Tests
{
    public class ColorRowConversionTests
    {
        [Theory]
        [InlineData(8, false)]
        [InlineData(8, true)]
        [InlineData(10, false)]
        [InlineData(10, true)]
        public void RowsMatchScalarConversionIncludingUnalignedBuffersAndTails(int depth, bool fullRange)
        {
            const byte Guard = 173;
            int maximum = (1 << depth) - 1;
            int lanes = Vector<ushort>.Count;
            int[] widths = { 0, 1, 2, 3, lanes - 1, lanes, lanes + 1,
                2 * lanes - 1, 2 * lanes, 2 * lanes + 1, 65, 257, 512, 1023 };
            Random random = new Random(724);
            foreach (int matrix in new[] { 1, 2, 5, 6, 9 })
            {
                ColorConversion conversion = new ColorConversion(depth, fullRange, matrix);
                foreach (int subWidth in new[] { 1, 2 })
                {
                    foreach (int width in widths)
                    {
                        int chromaSamples = (width + subWidth - 1) / subWidth;
                        ushort[] luma = Samples(width, maximum, random);
                        ushort[] cb = Samples(chromaSamples, maximum, random);
                        ushort[] cr = Samples(chromaSamples, maximum, random);
                        ushort[] savedLuma = (ushort[])luma.Clone();
                        ushort[] savedCb = (ushort[])cb.Clone();
                        ushort[] savedCr = (ushort[])cr.Clone();
                        byte[] expected = new byte[width * 4 + 10];
                        byte[] actual = new byte[expected.Length];
                        Array.Fill(expected, Guard);
                        Array.Fill(actual, Guard);
                        for (int x = 0; x < width; x++)
                        {
                            conversion.Convert(luma[x + 1], cb[x / subWidth + 1], cr[x / subWidth + 1],
                                expected.AsSpan(3 + x * 4, 4));
                        }
                        ColorPlaneLayout layout = new ColorPlaneLayout(width, 1, width, chromaSamples, subWidth, 1);
                        conversion.ConvertRows(luma.AsSpan(1, width), cb.AsSpan(1, chromaSamples),
                            cr.AsSpan(1, chromaSamples), layout, actual.AsSpan(3, width * 4));
                        Assert.Equal(expected, actual);
                        Assert.Equal(savedLuma, luma);
                        Assert.Equal(savedCb, cb);
                        Assert.Equal(savedCr, cr);
                    }
                }
            }
        }

        [Fact]
        public void VectorRoundingIsExactOnBothSidesOfEveryChannelHalfStep()
        {
            const int Scale = 1 << 20;
            ColorConversionVectors conversion = new ColorConversionVectors(0, 0, 1, 0, 0, 0, 0);
            int[] fractions = { -Scale / 2 - 1, -Scale / 2, -Scale / 2 + 1, -1, 0, 1,
                Scale / 2 - 1, Scale / 2, Scale / 2 + 1, Scale - 1 };
            int[] values = new int[Vector<int>.Count];
            int[] packed = new int[values.Length];
            for (int level = -1; level <= 256; level++)
            {
                foreach (int fraction in fractions)
                {
                    for (int lane = 0; lane < values.Length; lane++)
                    {
                        values[lane] = (level + lane) * Scale + fraction;
                    }
                    conversion.Convert(new Vector<int>(values), Vector<int>.Zero, Vector<int>.Zero).CopyTo(packed);
                    ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(packed.AsSpan());
                    for (int lane = 0; lane < values.Length; lane++)
                    {
                        byte expected = (byte)Math.Clamp(((long)values[lane] + Scale / 2) >> 20, 0, 255);
                        Assert.Equal(expected, bytes[lane * 4]);
                        Assert.Equal(expected, bytes[lane * 4 + 1]);
                        Assert.Equal(expected, bytes[lane * 4 + 2]);
                        Assert.Equal(255, bytes[lane * 4 + 3]);
                    }
                }
            }
        }

        [Fact]
        public void RowsRejectIncompleteBuffersAndUnsupportedSampling()
        {
            ColorConversion conversion = new ColorConversion(8, false, 1);
            ushort[] luma = new ushort[65];
            ushort[] chroma = new ushort[33];
            byte[] output = new byte[260];
            ColorPlaneLayout layout = new ColorPlaneLayout(65, 1, 65, 33, 2, 1);
            Assert.Throws<ArgumentOutOfRangeException>(() => new ColorPlaneLayout(65, 1, 65, 33, 0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ColorPlaneLayout(65, 1, 65, 33, 3, 1));
            Assert.Throws<ArgumentException>(() => conversion.ConvertRows(luma, new ushort[32], chroma, layout, output));
            Assert.Throws<ArgumentException>(() => conversion.ConvertRows(luma, chroma, new ushort[32], layout, output));
            Assert.Throws<ArgumentException>(() => conversion.ConvertRows(luma, chroma, chroma, layout, new byte[259]));
        }

        private static ushort[] Samples(int count, int maximum, Random random)
        {
            ushort[] samples = new ushort[count + 2];
            samples[0] = ushort.MaxValue;
            samples[^1] = ushort.MaxValue;
            for (int i = 0; i < count; i++)
            {
                samples[i + 1] = (ushort)((i % 8) switch
                {
                    0 => 0,
                    1 => maximum,
                    2 => maximum / 2,
                    3 => maximum / 2 + 1,
                    _ => random.Next(maximum + 1)
                });
            }
            return samples;
        }
    }
}
