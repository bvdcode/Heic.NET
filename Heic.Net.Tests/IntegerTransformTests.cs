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
            for (int sample = 0; sample < 12; sample++)
            {
                int[] coefficients = new int[size * size];
                for (int i = 0; i < coefficients.Length; i++)
                {
                    coefficients[i] = sample == 0 ? 0 : random.Next(short.MinValue, short.MaxValue + 1);
                }

                int[] result = new int[coefficients.Length];
                IntegerTransform.Inverse(coefficients, result, size, depth, sine, workspace);
                Assert.Equal(TransformOracle.Apply(coefficients, size, depth, sine), result);
                IntegerTransform.Inverse(coefficients, coefficients, size, depth, sine, workspace);
                Assert.Equal(result, coefficients);
            }
        }

        [Fact]
        public void DequantizationMatchesDecimalArithmeticAtLimits()
        {
            int[] factors = { 40, 45, 51, 57, 64, 72 };
            foreach (int depth in new[] { 8, 10 })
            {
                for (int qp = 0; qp <= 51 + 6 * (depth - 8); qp++)
                {
                    for (int log2 = 2; log2 <= 5; log2++)
                    {
                        foreach (int value in new[] { -32768, -1, 0, 1, 32767 })
                        {
                            decimal divisor = (decimal)Math.Pow(2, depth + log2 - 5);
                            decimal product = value * 255m * factors[qp % 6] * (decimal)Math.Pow(2, qp / 6);
                            int expected = (int)Math.Clamp(decimal.Floor((product + divisor / 2) / divisor), short.MinValue, short.MaxValue);
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
    }
}
