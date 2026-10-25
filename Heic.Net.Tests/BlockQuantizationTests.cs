// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using System;
using Heic.Net.Hevc;
using Xunit;

namespace Heic.Net.Tests
{
    public class BlockQuantizationTests
    {
        [Theory]
        [InlineData(8, 0)]
        [InlineData(8, 1)]
        [InlineData(8, 2)]
        [InlineData(10, 0)]
        [InlineData(10, 1)]
        [InlineData(10, 2)]
        public void CompleteBlocksMatchDecimalArithmeticForEveryQuantizer(int depth, int listType)
        {
            ScalingLists lists = Lists(listType);
            int[] scales = { 40, 45, 51, 57, 64, 72 };
            int[] extremes = { short.MinValue, -1, 0, 1, short.MaxValue };
            Random random = new Random(981);
            for (int log2Size = 2; log2Size <= 5; log2Size++)
            {
                int size = 1 << log2Size;
                int count = size * size;
                decimal divisor = (decimal)Math.Pow(2, depth + log2Size - 5);
                for (int matrixId = 0; matrixId < 6; matrixId++)
                {
                    for (int qp = 0; qp <= 51 + 6 * (depth - 8); qp++)
                    {
                        int[] coefficients = new int[count + 1];
                        int[] expected = new int[count + 1];
                        decimal multiplier = scales[qp % 6] * (decimal)Math.Pow(2, qp / 6);
                        for (int i = 0; i < count; i++)
                        {
                            int coefficient = i % 7 < extremes.Length ? extremes[i % 7] : random.Next(short.MinValue, short.MaxValue + 1);
                            coefficients[i] = coefficient;
                            decimal product = coefficient * multiplier * lists.Factor(log2Size, matrixId, i % size, i / size);
                            expected[i] = (int)Math.Clamp(decimal.Floor((product + divisor / 2) / divisor), short.MinValue, short.MaxValue);
                        }
                        expected[count] = coefficients[count] = int.MaxValue;
                        CoefficientMath.Dequantize(coefficients, lists, matrixId, qp, log2Size, depth);
                        Assert.Equal(expected, coefficients);
                    }
                }
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public void RejectsInvalidParametersAndCoefficientPrecision(int listType)
        {
            ScalingLists lists = Lists(listType);
            Assert.Throws<ArgumentException>(() => CoefficientMath.Dequantize(new int[15], lists, 0, 0, 2, 8));
            Assert.Throws<ArgumentOutOfRangeException>(() => CoefficientMath.Dequantize(new int[16], lists, -1, 0, 2, 8));
            Assert.Throws<ArgumentOutOfRangeException>(() => CoefficientMath.Dequantize(new int[16], lists, 6, 0, 2, 8));
            Assert.Throws<ArgumentOutOfRangeException>(() => CoefficientMath.Dequantize(new int[1024], lists, 6, 0, 5, 8));
            Assert.Throws<ArgumentOutOfRangeException>(() => CoefficientMath.Dequantize(new int[16], lists, 0, -1, 2, 8));
            Assert.Throws<ArgumentOutOfRangeException>(() => CoefficientMath.Dequantize(new int[16], lists, 0, 52, 2, 8));
            Assert.Throws<ArgumentOutOfRangeException>(() => CoefficientMath.Dequantize(new int[16], lists, 0, 64, 2, 10));
            Assert.Throws<ArgumentOutOfRangeException>(() => CoefficientMath.Dequantize(new int[16], lists, 0, 0, 1, 8));
            Assert.Throws<ArgumentOutOfRangeException>(() => CoefficientMath.Dequantize(new int[4096], lists, 0, 0, 6, 8));
            Assert.Throws<ArgumentOutOfRangeException>(() => CoefficientMath.Dequantize(new int[16], lists, 0, 0, 2, 9));
            foreach (int invalid in new[] { short.MinValue - 1, short.MaxValue + 1 })
            {
                int[] coefficients = new int[16];
                coefficients[15] = invalid;
                Assert.Throws<ArgumentOutOfRangeException>(() => CoefficientMath.Dequantize(coefficients, lists, 0, 0, 2, 8));
            }
        }

        private static ScalingLists Lists(int type)
        {
            switch (type)
            {
                case 0:
                    return ScalingLists.Flat;
                case 1:
                    return ScalingLists.Default;
                case 2:
                    TestBitWriter writer = new TestBitWriter();
                    for (int sizeId = 0; sizeId < 4; sizeId++)
                    {
                        for (int matrixId = 0; matrixId < 6; matrixId += sizeId == 3 ? 3 : 1)
                        {
                            writer.Flag(true);
                            if (sizeId > 1)
                            {
                                writer.Se(247);
                            }
                            for (int i = 0; i < (sizeId == 0 ? 16 : 64); i++)
                            {
                                writer.Se(i == 0 ? (sizeId > 1 ? 1 : -8) : (i % 2 == 0 ? -128 : 127));
                            }
                        }
                    }
                    return ScalingLists.Read(new BitReader(writer.ToArray()));
                default:
                    throw new ArgumentOutOfRangeException(nameof(type));
            }
        }
    }
}
