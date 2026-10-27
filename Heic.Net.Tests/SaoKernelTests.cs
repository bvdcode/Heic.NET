// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov (bvdcode) <https://belov.us>

using Heic.Net.Hevc;
using Xunit;

namespace Heic.Net.Tests
{
    public class SaoKernelTests
    {
        [Fact]
        public void BandOffsetsWrapAtTheLastIntensityBand()
        {
            SaoParameters parameters = new SaoParameters(1, 30, 0, 3, -2, 4, -5);
            Assert.Equal(243, SaoKernel.Band(240, 8, parameters));
            Assert.Equal(246, SaoKernel.Band(248, 8, parameters));
            Assert.Equal(4, SaoKernel.Band(0, 8, parameters));
            Assert.Equal(3, SaoKernel.Band(8, 8, parameters));
            Assert.Equal(16, SaoKernel.Band(16, 8, parameters));
        }

        [Theory]
        [InlineData(12, 12, 5)]
        [InlineData(12, 10, 3)]
        [InlineData(10, 12, 3)]
        [InlineData(12, 8, 0)]
        [InlineData(8, 12, 0)]
        [InlineData(8, 10, -4)]
        [InlineData(10, 8, -4)]
        [InlineData(12, 11, 5)]
        [InlineData(9, 8, -6)]
        public void EdgeCategoriesFollowNeighbourOrdering(int first, int second, int expectedOffset)
        {
            SaoParameters parameters = new SaoParameters(2, 0, 0, 5, 3, -4, -6);
            Assert.Equal(10 + expectedOffset, SaoKernel.Edge(10, first, second, 8, parameters));
        }

        [Fact]
        public void OffsetsClipAtComponentLimits()
        {
            SaoParameters parameters = new SaoParameters(1, 31, 0, 7, -7, 0, 0);
            Assert.Equal(255, SaoKernel.Band(255, 8, parameters));
            Assert.Equal(0, SaoKernel.Band(0, 8, parameters));
        }
    }
}
