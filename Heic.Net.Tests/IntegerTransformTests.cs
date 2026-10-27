// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov (bvdcode) <https://belov.us>

using System;
using Heic.Net.Hevc;
using Xunit;

namespace Heic.Net.Tests
{
    public class IntegerTransformTests
    {
        [Theory]
        [InlineData(4, 8, false)]
        [InlineData(8, 8, false)]
        [InlineData(16, 8, false)]
        [InlineData(32, 8, false)]
        [InlineData(4, 10, false)]
        [InlineData(8, 10, false)]
        [InlineData(16, 10, false)]
        [InlineData(32, 10, false)]
        [InlineData(4, 8, true)]
        [InlineData(4, 10, true)]
        public void MatchesIndependentMatrixOracle(int size, int depth, bool sine)
        {
            Random random = new Random(173);
            using TransformWorkspace workspace = new TransformWorkspace();
            for (int sample = 0; sample < 20; sample++)
            {
                int[] coefficients = new int[size * size];
                for (int i = 0; i < coefficients.Length; i++)
                {
                    coefficients[i] = sample switch
                    {
                        0 => 0,
                        1 => short.MinValue,
                        2 => short.MaxValue,
                        3 => (i & 1) == 0 ? short.MinValue : short.MaxValue,
                        4 => i == 0 ? short.MinValue : 0,
                        5 => i == (coefficients.Length - 1) ? short.MaxValue : 0,
                        6 => i < size ? random.Next(short.MinValue, short.MaxValue + 1) : 0,
                        7 => (i % size) == (size - 1) ? random.Next(short.MinValue, short.MaxValue + 1) : 0,
                        _ => random.Next(short.MinValue, short.MaxValue + 1)
                    };
                }

                int[] result = new int[coefficients.Length];
                IntegerTransform.Inverse(coefficients, result, size, depth, sine, workspace);
                Assert.Equal(TransformOracle.Apply(coefficients, size, depth, sine), result);
                IntegerTransform.Inverse(coefficients, coefficients, size, depth, sine, workspace);
                Assert.Equal(result, coefficients);
            }
        }

        [Theory]
        [InlineData(8, false)]
        [InlineData(10, false)]
        [InlineData(8, true)]
        [InlineData(10, true)]
        public void FourPointImpulsesPreserveRoundingAndDestinationBounds(int depth, bool sine)
        {
            const int Size = 4;
            const int Count = Size * Size;
            const int Guard = 123456789;
            int[] levels = { -32768, -16385, -1025, -129, -128, -127, -65, -64, -63, -1,
                0, 1, 63, 64, 65, 127, 128, 129, 1023, 1024, 16383, 32767 };
            using TransformWorkspace workspace = new TransformWorkspace();
            for (int position = 0; position < Count; position++)
            {
                foreach (int level in levels)
                {
                    int[] coefficients = new int[Count];
                    coefficients[position] = level;
                    int[] result = new int[Count + 8];
                    Array.Fill(result, Guard);
                    workspace.Intermediate.Fill(Guard);
                    IntegerTransform.Inverse(coefficients, result, Size, depth, sine, workspace);
                    Assert.Equal(TransformOracle.Apply(coefficients, Size, depth, sine), result.AsSpan(0, Count).ToArray());
                    Assert.All(result.AsSpan(Count).ToArray(), value => Assert.Equal(Guard, value));
                    Assert.All(workspace.Intermediate.Slice(Count).ToArray(), value => Assert.Equal(Guard, value));
                }
            }
        }

        [Fact]
        public void DequantizationMatchesDecimalArithmeticAtLimits()
        {
            int[] factors = { 40, 45, 51, 57, 64, 72 };
            foreach (int depth in new[] { 8, 10 })
            {
                for (int qp = 0; qp <= (51 + (6 * (depth - 8))); qp++)
                {
                    for (int log2 = 2; log2 <= 5; log2++)
                    {
                        foreach (int value in new[] { -32768, -1, 0, 1, 32767 })
                        {
                            decimal divisor = (decimal)Math.Pow(2, (depth + log2) - 5);
                            decimal product = value * 255m * factors[qp % 6] * (decimal)Math.Pow(2, qp / 6);
                            int expected = (int)Math.Clamp(decimal.Floor((product + (divisor / 2)) / divisor), short.MinValue, short.MaxValue);
                            Assert.Equal(expected, CoefficientMath.Dequantize(value, 255, qp, log2, depth));
                        }
                    }
                }
            }
        }

        [Fact]
        public void DisposedWorkspaceCannotBeUsed()
        {
            TransformWorkspace workspace = new TransformWorkspace();
            workspace.Dispose();
            workspace.Dispose();
            Assert.Throws<ObjectDisposedException>(() => workspace.Intermediate.Length);
        }

        [Theory]
        [InlineData(8)]
        [InlineData(10)]
        public void ReusedScratchPreservesShrinkingAndEmptyCoefficientExtents(int depth)
        {
            const int Guard = 123456789;
            using TransformWorkspace workspace = new TransformWorkspace();
            int[] result = new int[(32 * 32) + 8];
            foreach (int size in new[] { 32, 16, 8, 4, 8, 16, 32 })
            {
                int count = size * size;
                int[] coefficients = new int[count];
                foreach (int extent in new[] { size, size / 2, 1, 0 })
                {
                    Array.Clear(coefficients);
                    for (int y = 0; y < extent; y++)
                    {
                        for (int x = 0; x < extent; x++)
                        {
                            coefficients[(y * size) + x] = ((x + y) & 1) == 0 ? short.MaxValue : short.MinValue;
                        }
                    }
                    Array.Fill(result, Guard);
                    workspace.Intermediate.Fill(Guard);
                    IntegerTransform.Inverse(coefficients, result.AsSpan(4, count), size, depth, false, workspace);
                    Assert.Equal(TransformOracle.Apply(coefficients, size, depth, false), result.AsSpan(4, count).ToArray());
                    Assert.All(result.AsSpan(0, 4).ToArray(), value => Assert.Equal(Guard, value));
                    Assert.All(result.AsSpan(count + 4).ToArray(), value => Assert.Equal(Guard, value));
                }
            }
        }
    }
}
