// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using System;
using Heic.Net.Hevc;
using Xunit;

namespace Heic.Net.Tests
{
    public class FlatQuantizationTests
    {
        [Theory]
        [InlineData(8)]
        [InlineData(10)]
        public void EverySignedCoefficientMatchesWideArithmeticForEveryQuantizerAndSize(int depth)
        {
            int[] scales = { 40, 45, 51, 57, 64, 72 };
            for (int log2Size = 2; log2Size <= 5; log2Size++)
            {
                int count = 1 << (2 * log2Size);
                int[] coefficients = new int[count + 1];
                int[] expected = new int[count + 1];
                expected[count] = coefficients[count] = int.MaxValue;
                int shift = (depth + log2Size) - 5;
                long rounding = 1L << (shift - 1);
                for (int qp = 0; qp <= (51 + (6 * (depth - 8))); qp++)
                {
                    long multiplier = 16L * scales[qp % 6] * (1L << (qp / 6));
                    for (int start = short.MinValue; start <= short.MaxValue; start += count)
                    {
                        for (int i = 0; i < count; i++)
                        {
                            int coefficient = start + i;
                            coefficients[i] = coefficient;
                            long result = ((coefficient * multiplier) + rounding) >> shift;
                            expected[i] = (int)Math.Clamp(result, short.MinValue, short.MaxValue);
                        }
                        CoefficientMath.Dequantize(coefficients, ScalingLists.Flat, 0, qp, log2Size, depth);
                        Assert.Equal(expected, coefficients);
                    }
                }
            }
        }
    }
}
