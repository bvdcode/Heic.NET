// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov (bvdcode) <https://belov.us>

using System;
using Heic.Net.Hevc;
using Xunit;

namespace Heic.Net.Tests
{
    public class CoefficientContextTests
    {
        private static readonly int[][] LocalContexts =
        {
            new[] { 2, 1, 1, 0, 1, 1, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0 },
            new[] { 2, 2, 2, 2, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0 },
            new[] { 2, 1, 0, 0, 2, 1, 0, 0, 2, 1, 0, 0, 2, 1, 0, 0 },
            new[] { 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2 }
        };
        private static readonly int[] FourPointContexts = { 0, 1, 4, 5, 2, 3, 4, 5, 6, 6, 8, 8, 7, 7, 8, 8 };

        [Theory]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        public void EveryGroupUsesTheNormativeContextsInEachScanOrder(int log2Size)
        {
            int size = 1 << log2Size;
            int width = size / 4;
            for (int scanIndex = 0; scanIndex < 3; scanIndex++)
            {
                ReadOnlySpan<int> scan = CoefficientScan.Block(log2Size, scanIndex);
                for (int component = 0; component < 3; component++)
                {
                    for (int group = 0; group < (width * width); group++)
                    {
                        int first = scan[group * 16];
                        int gx = (first % size) / 4;
                        int gy = first / size / 4;
                        for (int right = 0; right < 2; right++)
                        {
                            for (int below = 0; below < 2; below++)
                            {
                                byte[] neighbours = new byte[width * width];
                                Array.Fill(neighbours, (byte)1);
                                int pattern = 0;
                                if ((gx + 1) < width)
                                {
                                    neighbours[(gy * width) + gx + 1] = (byte)right;
                                    pattern += right;
                                }
                                if ((gy + 1) < width)
                                {
                                    neighbours[((gy + 1) * width) + gx] = (byte)below;
                                    pattern += 2 * below;
                                }
                                ReadOnlySpan<byte> actual = CoefficientContexts.SignificantGroup(gx, gy,
                                    log2Size, scanIndex, component, neighbours);
                                for (int n = 0; n < 16; n++)
                                {
                                    int offset = scan[(group * 16) + n];
                                    int local = (((offset / size) % 4) * 4) + (offset % 4);
                                    int expected = ExpectedContext(log2Size, offset, local, component, group, scanIndex, pattern);
                                    Assert.Equal(expected + (component == 0 ? 0 : 27), actual[n]);
                                }
                            }
                        }
                    }
                }
            }
        }

        private static int ExpectedContext(int log2Size, int offset, int local, int component, int group, int scanIndex, int pattern)
        {
            int expected;
            if (log2Size == 2)
            {
                expected = FourPointContexts[local];
            }
            else if (offset == 0)
            {
                expected = 0;
            }
            else
            {
                int basis = component == 0
                    ? log2Size == 3 ? scanIndex == 0 ? 9 : 15 : 21
                    : log2Size == 3 ? 9 : 12;
                expected = basis + LocalContexts[pattern][local];
                if ((component == 0) && (group != 0))
                {
                    expected += 3;
                }
            }
            return expected;
        }
    }
}
