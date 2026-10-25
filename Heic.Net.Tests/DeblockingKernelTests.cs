// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using System;
using Heic.Net.Hevc;
using Xunit;

namespace Heic.Net.Tests
{
    public class DeblockingKernelTests
    {
        private static readonly int[,] StrongWeights =
        {
            { 2,3,1,1,1,0,0,0 },
            { 0,1,1,1,1,0,0,0 },
            { 0,1,2,2,2,1,0,0 },
            { 0,0,1,2,2,2,1,0 },
            { 0,0,0,1,1,1,1,0 },
            { 0,0,0,1,1,1,3,2 }
        };

        [Theory]
        [InlineData(8)]
        [InlineData(10)]
        public void StrongFilterMatchesDenseConvolution(int depth)
        {
            Random random = new Random(11827);
            for (int repetition = 0; repetition < 100; repetition++)
            {
                int[] original = new int[32];
                for (int row = 0; row < 4; row++)
                {
                    int p = random.Next(10, (1 << depth) - 10);
                    int q = p + random.Next(-3, 4);
                    for (int i = 0; i < 8; i++)
                    {
                        original[row * 8 + i] = i < 4 ? p : q;
                    }
                }

                int[] actual = (int[])original.Clone();
                DeblockingKernel.Luma(actual, 64, 20, depth, true, true);
                for (int row = 0; row < 4; row++)
                {
                    for (int output = 0; output < 6; output++)
                    {
                        int sum = 0;
                        int denominator = 0;
                        for (int input = 0; input < 8; input++)
                        {
                            sum += original[row * 8 + input] * StrongWeights[output, input];
                            denominator += StrongWeights[output, input];
                        }

                        int position = row * 8 + output + 1;
                        int rounded = (int)Math.Floor(sum / (double)denominator + 0.5);
                        Assert.Equal(Math.Clamp(rounded, original[position] - 40, original[position] + 40), actual[position]);
                    }
                }
            }
        }

        [Fact]
        public void WeakFilterUsesOriginalSamplesAndSignedRounding()
        {
            int[] original = { 90, 95, 98, 100, 110, 111, 113, 120 };
            int[] expected = { 90, 95, 99, 103, 107, 110, 113, 120 };
            int[] actual = new int[32];
            for (int row = 0; row < 4; row++)
            {
                original.CopyTo(actual, row * 8);
            }
            DeblockingKernel.Luma(actual, 32, 4, 8, true, true);
            for (int row = 0; row < 4; row++)
            {
                Assert.Equal(expected, actual.AsSpan(row * 8, 8).ToArray());
            }
        }

        [Fact]
        public void BypassSideIsPreservedEvenWhenOtherSideIsFiltered()
        {
            int[] rows = new int[32];
            for (int row = 0; row < 4; row++)
            {
                for (int i = 0; i < 8; i++)
                {
                    rows[row * 8 + i] = i < 4 ? 100 : 104;
                }
            }

            DeblockingKernel.Luma(rows, 64, 20, 8, false, true);
            for (int row = 0; row < 4; row++)
            {
                for (int i = 0; i < 4; i++)
                {
                    Assert.Equal(100, rows[row * 8 + i]);
                }
                Assert.Equal(103, rows[row * 8 + 4]);
            }
        }

        [Theory]
        [InlineData(8)]
        [InlineData(10)]
        public void ChromaMatchesScalarRationalOracle(int depth)
        {
            Random random = new Random(352);
            for (int repetition = 0; repetition < 200; repetition++)
            {
                int[] row = new int[4];
                for (int i = 0; i < 4; i++)
                {
                    row[i] = random.Next(1 << depth);
                }
                int[] original = (int[])row.Clone();
                int delta = Math.Clamp((int)Math.Floor((4.0 * (row[2] - row[1]) + row[0] - row[3]) / 8 + 0.5), -12, 12);
                DeblockingKernel.Chroma(row, 12, depth, true, true);
                Assert.Equal(Math.Clamp(original[1] + delta, 0, (1 << depth) - 1), row[1]);
                Assert.Equal(Math.Clamp(original[2] - delta, 0, (1 << depth) - 1), row[2]);
            }
        }
    }
}
