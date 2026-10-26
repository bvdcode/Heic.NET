// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using System;
using System.Numerics;
using System.Linq;
using Heic.Net.Hevc;
using Xunit;

namespace Heic.Net.Tests
{
    public class ColorPlaneConversionTests
    {
        [Theory]
        [InlineData(8, false)]
        [InlineData(8, true)]
        [InlineData(10, false)]
        [InlineData(10, true)]
        [InlineData(8, false, 1, 0)]
        [InlineData(8, true, 0, 1)]
        [InlineData(8, false, 1, 1)]
        [InlineData(8, true, 1, 1)]
        [InlineData(10, false, 1, 0)]
        [InlineData(10, true, 0, 1)]
        [InlineData(10, false, 1, 1)]
        [InlineData(10, true, 1, 1)]
        public void StridedPlanesMatchScalarPixelsWithoutReadingPadding(int depth, bool fullRange, int phaseX = 0, int phaseY = 0)
        {
            const int LumaOffset = 3;
            const int ChromaOffset = 5;
            int lanes = Vector<ushort>.Count;
            int[] widths = { 1, lanes - 1, lanes, lanes + 1, 2 * lanes - 1, 2 * lanes, 2 * lanes + 1, 4 * lanes - 1, 4 * lanes, 4 * lanes + 1, 65 };
            Random random = new Random(5107);
            foreach (int matrix in new[] { 1, 2, 5, 6, 9 })
            {
                ColorConversion conversion = new ColorConversion(depth, fullRange, matrix);
                foreach (int subWidth in new[] { 1, 2 })
                {
                    foreach (int subHeight in new[] { 1, 2 })
                    {
                        if (phaseX >= subWidth || phaseY >= subHeight)
                        {
                            continue;
                        }
                        foreach (int width in widths)
                        {
                            foreach (int height in new[] { 1, 2, 3, 17 })
                            {
                                int chromaWidth = (width + phaseX + subWidth - 1) / subWidth;
                                int chromaHeight = (height + phaseY + subHeight - 1) / subHeight;
                                ColorPlaneLayout layout = new ColorPlaneLayout(width, height, width + 7, chromaWidth + 5, subWidth, subHeight, phaseX, phaseY);
                                ushort[] y = Plane(width, height, layout.LumaStride, LumaOffset, depth, random);
                                ushort[] cb = Plane(chromaWidth, chromaHeight, layout.ChromaStride, ChromaOffset, depth, random);
                                ushort[] cr = Plane(chromaWidth, chromaHeight, layout.ChromaStride, ChromaOffset, depth, random);
                                ushort[] savedY = (ushort[])y.Clone();
                                ushort[] savedCb = (ushort[])cb.Clone();
                                ushort[] savedCr = (ushort[])cr.Clone();
                                byte[] expected = new byte[layout.OutputLength + 8];
                                byte[] actual = new byte[expected.Length];
                                Array.Fill(expected, (byte)173);
                                Array.Fill(actual, (byte)173);
                                for (int row = 0; row < height; row++)
                                {
                                    for (int x = 0; x < width; x++)
                                    {
                                        int chroma = ChromaOffset + (row + phaseY) / subHeight * layout.ChromaStride + (x + phaseX) / subWidth;
                                        conversion.Convert(y[LumaOffset + row * layout.LumaStride + x], cb[chroma], cr[chroma],
                                            expected.AsSpan(3 + (row * width + x) * 4, 4));
                                    }
                                }
                                conversion.ConvertRows(y.AsSpan(LumaOffset, layout.LumaLength),
                                    cb.AsSpan(ChromaOffset, layout.ChromaLength), cr.AsSpan(ChromaOffset, layout.ChromaLength),
                                    layout, actual.AsSpan(3, layout.OutputLength));
                                Assert.Equal(expected, actual);
                                Assert.Equal(savedY, y);
                                Assert.Equal(savedCb, cb);
                                Assert.Equal(savedCr, cr);
                                if (depth == 8)
                                {
                                    byte[] yBytes = y.Select(value => (byte)value).ToArray();
                                    byte[] cbBytes = cb.Select(value => (byte)value).ToArray();
                                    byte[] crBytes = cr.Select(value => (byte)value).ToArray();
                                    Array.Fill(actual, (byte)173);
                                    conversion.ConvertRows(yBytes.AsSpan(LumaOffset, layout.LumaLength),
                                        cbBytes.AsSpan(ChromaOffset, layout.ChromaLength), crBytes.AsSpan(ChromaOffset, layout.ChromaLength),
                                        layout, actual.AsSpan(3, layout.OutputLength));
                                    Assert.Equal(expected, actual);
                                    Assert.Equal(savedY.Select(value => (byte)value), yBytes);
                                    Assert.Equal(savedCb.Select(value => (byte)value), cbBytes);
                                    Assert.Equal(savedCr.Select(value => (byte)value), crBytes);
                                }
                            }
                        }
                    }
                }
            }
        }

        [Fact]
        public void EmptyPlanesAndInvalidLayoutsAreHandledBeforeReadingSamples()
        {
            ColorConversion conversion = new ColorConversion(8, false, 1);
            byte[] output = { 11, 22, 33 };
            conversion.ConvertRows(ReadOnlySpan<ushort>.Empty, ReadOnlySpan<ushort>.Empty, ReadOnlySpan<ushort>.Empty, new ColorPlaneLayout(0, 0, 0, 0, 2, 2), output);
            conversion.ConvertRows(ReadOnlySpan<ushort>.Empty, ReadOnlySpan<ushort>.Empty, ReadOnlySpan<ushort>.Empty, new ColorPlaneLayout(0, 1, 0, 0, 2, 2), output);
            Assert.Equal(new byte[] { 11, 22, 33 }, output);
            Assert.Throws<ArgumentOutOfRangeException>(() => new ColorPlaneLayout(-1, 1, 0, 0, 1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ColorPlaneLayout(1, -1, 1, 1, 1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ColorPlaneLayout(5, 3, 4, 3, 2, 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ColorPlaneLayout(5, 3, 5, 2, 2, 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ColorPlaneLayout(5, 3, 5, 3, 2, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ColorPlaneLayout(5, 3, 5, 3, 2, 3));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ColorPlaneLayout(5, 3, 5, 3, 2, 2, -1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ColorPlaneLayout(5, 3, 5, 3, 2, 2, 2, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ColorPlaneLayout(5, 3, 5, 3, 2, 2, 0, 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ColorPlaneLayout(5, 3, 5, 5, 1, 1, 1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ColorPlaneLayout(6, 3, 6, 3, 2, 2, 1, 0));
            Assert.Throws<OverflowException>(() => new ColorPlaneLayout(65536, 65536, 65536, 32768, 2, 2));
            ColorPlaneLayout layout = new ColorPlaneLayout(5, 3, 8, 4, 2, 2);
            Assert.Throws<ArgumentException>(() => conversion.ConvertRows(new ushort[20], new ushort[7], new ushort[7], layout, new byte[60]));
            Assert.Throws<ArgumentException>(() => conversion.ConvertRows(new ushort[21], new ushort[6], new ushort[7], layout, new byte[60]));
            Assert.Throws<ArgumentException>(() => conversion.ConvertRows(new ushort[21], new ushort[7], new ushort[6], layout, new byte[60]));
            Assert.Throws<ArgumentException>(() => conversion.ConvertRows(new ushort[21], new ushort[7], new ushort[7], layout, new byte[59]));
        }

        [Fact]
        public void BytePlanesRejectShortBuffersBeforeWritingOutput()
        {
            ColorConversion conversion = new ColorConversion(8, false, 1);
            ColorPlaneLayout layout = new ColorPlaneLayout(6, 3, 8, 4, 2, 2, 1, 1);
            byte[] output = new byte[72];
            Array.Fill(output, (byte)173);
            Assert.Throws<ArgumentException>(() => conversion.ConvertRows(new byte[21], new byte[8], new byte[8], layout, output));
            Assert.Throws<ArgumentException>(() => conversion.ConvertRows(new byte[22], new byte[7], new byte[8], layout, output));
            Assert.Throws<ArgumentException>(() => conversion.ConvertRows(new byte[22], new byte[8], new byte[7], layout, output));
            Assert.Throws<ArgumentException>(() => conversion.ConvertRows(new byte[22], new byte[8], new byte[8], layout, output.AsSpan(0, 71)));
            Assert.All(output, value => Assert.Equal((byte)173, value));
            conversion.ConvertRows(ReadOnlySpan<byte>.Empty, ReadOnlySpan<byte>.Empty, ReadOnlySpan<byte>.Empty,
                new ColorPlaneLayout(0, 1, 0, 0, 2, 2, 1, 1), output);
            Assert.All(output, value => Assert.Equal((byte)173, value));
        }

        private static ushort[] Plane(int width, int height, int stride, int offset, int depth, Random random)
        {
            ushort[] samples = new ushort[offset + height * stride + 7];
            Array.Fill(samples, ushort.MaxValue);
            for (int row = 0; row < height; row++)
            {
                for (int x = 0; x < width; x++)
                {
                    samples[offset + row * stride + x] = (ushort)random.Next(1 << depth);
                }
            }
            return samples;
        }
    }
}
