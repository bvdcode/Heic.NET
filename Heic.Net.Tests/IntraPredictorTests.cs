// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using System;
using Heic.Net.Hevc;
using Xunit;

namespace Heic.Net.Tests
{
    public class IntraPredictorTests
    {
        [Theory]
        [InlineData(4, 8)]
        [InlineData(8, 8)]
        [InlineData(16, 8)]
        [InlineData(32, 8)]
        [InlineData(4, 10)]
        [InlineData(8, 10)]
        [InlineData(16, 10)]
        [InlineData(32, 10)]
        public void EveryModeMatchesCoordinateOracle(int size, int depth)
        {
            Random random = new Random(109 + size + depth);
            for (int repetition = 0; repetition < 6; repetition++)
            {
                int[] top = new int[(2 * size) + 1];
                int[] left = new int[(2 * size) + 1];
                for (int i = 0; i < top.Length; i++)
                {
                    top[i] = random.Next(1 << depth);
                    left[i] = random.Next(1 << depth);
                }

                left[0] = top[0];
                for (int mode = 0; mode <= 34; mode++)
                {
                    for (int component = 0; component <= 1; component++)
                    {
                        bool luma = component == 0;
                        int[] actual = new int[size * size];
                        IntraPredictor.Predict(top, left, actual, size, mode, depth, luma, true);
                        Assert.Equal(IntraOracle.Predict(top, left, size, mode, depth, luma, true), actual);
                    }
                }
            }
        }

        [Fact]
        public void StrongSmoothingInterpolatesTheWholeBorder()
        {
            int[] top = new int[65];
            int[] left = new int[65];
            for (int i = 0; i <= 64; i++)
            {
                top[i] = 100 + (i * 3) + ((i % 4) == 0 ? 1 : 0);
                left[i] = 100 + (i * 2) + ((i % 3) == 0 ? 1 : 0);
            }

            int[] expectedTop = (int[])top.Clone();
            int[] expectedLeft = (int[])left.Clone();
            IntraOracle.Filter(expectedTop, expectedLeft, 32, 0, 10, true);
            IntraReferenceFilter.Apply(top, left, 32, 0, 10, true);
            Assert.Equal(expectedTop, top);
            Assert.Equal(expectedLeft, left);
        }

        [Fact]
        public void SubstitutionPropagatesFromBottomLeftAcrossTheCorner()
        {
            int[] top = { -1, -1, -1, 80, -1, 90, -1, -1, -1 };
            int[] left = { -1, -1, -1, -1, -1, 40, -1, -1, -1 };
            IntraReferenceSamples.Substitute(top, left, 4, 8);
            Assert.Equal(new[] { 40, 40, 40, 80, 80, 90, 90, 90, 90 }, top);
            Assert.Equal(new[] { 40, 40, 40, 40, 40, 40, 40, 40, 40 }, left);
        }

        [Fact]
        public void MissingBordersUseTheBitDepthMidpoint()
        {
            int[] top = new int[9];
            int[] left = new int[9];
            Array.Fill(top, -1);
            Array.Fill(left, -1);
            IntraReferenceSamples.Substitute(top, left, 4, 10);
            Assert.All(top, value => Assert.Equal(512, value));
            Assert.All(left, value => Assert.Equal(512, value));
        }
    }
}
