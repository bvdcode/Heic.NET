// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using System;
using Heic.Net.Hevc;
using Xunit;

namespace Heic.Net.Tests
{
    public class ColorConversionTests
    {
        [Theory]
        [InlineData(8, false, 1, 0.2126, 0.0722)]
        [InlineData(10, false, 1, 0.2126, 0.0722)]
        [InlineData(8, true, 6, 0.299, 0.114)]
        [InlineData(10, true, 6, 0.299, 0.114)]
        [InlineData(10, false, 9, 0.2627, 0.0593)]
        public void FixedPointConversionMatchesIndependentFloatingPoint(int depth, bool fullRange, int matrix,
            double kr, double kb)
        {
            ColorConversion conversion = new ColorConversion(depth, fullRange, matrix);
            Random random = new Random(183);
            int maximum = (1 << depth) - 1;
            int unit = 1 << (depth - 8);
            Span<byte> rgba = stackalloc byte[4];
            for (int i = 0; i < 10000; i++)
            {
                int y = random.Next(maximum + 1);
                int cb = random.Next(maximum + 1);
                int cr = random.Next(maximum + 1);
                double luma = (y - (fullRange ? 0 : 16 * unit)) / (double)(fullRange ? maximum : 219 * unit);
                double blue = (cb - (1 << (depth - 1))) / (double)(fullRange ? maximum : 224 * unit);
                double red = (cr - (1 << (depth - 1))) / (double)(fullRange ? maximum : 224 * unit);
                double r = luma + 2 * (1 - kr) * red;
                double b = luma + 2 * (1 - kb) * blue;
                double g = (luma - kr * r - kb * b) / (1 - kr - kb);
                conversion.Convert(y, cb, cr, rgba);
                Assert.InRange(Math.Abs(rgba[0] - Reference(r)), 0, 1);
                Assert.InRange(Math.Abs(rgba[1] - Reference(g)), 0, 1);
                Assert.InRange(Math.Abs(rgba[2] - Reference(b)), 0, 1);
                Assert.Equal(255, rgba[3]);
            }
        }

        [Theory]
        [InlineData(8)]
        [InlineData(10)]
        public void LimitedRangeNeutralEndpointsAreExact(int depth)
        {
            ColorConversion conversion = new ColorConversion(depth, false, 1);
            int unit = 1 << (depth - 8);
            Assert.Equal(0, conversion.Gray(16 * unit));
            Assert.Equal(255, conversion.Gray(235 * unit));
            Assert.Equal(0, conversion.Gray(0));
            Assert.Equal(255, conversion.Gray((1 << depth) - 1));
        }

        [Fact]
        public void UnspecifiedMatrixDoesNotSelectAnImplicitColorModel()
        {
            Assert.Throws<UnsupportedHeicFeatureException>(() => new ColorConversion(8, false, 2));
        }

        private static int Reference(double value) => (int)Math.Min(255, Math.Max(0, Math.Floor(value * 255 + 0.5)));
    }
}
