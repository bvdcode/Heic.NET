// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using System;
using Heic.Net.Hevc;
using Xunit;

namespace Heic.Net.Tests
{
    public class CodingBlockQuantizerTests
    {
        [Fact]
        public void QuantizersPreserveSignedValuesAndUnwrittenNeighbours()
        {
            using CodingBlockMap blocks = new CodingBlockMap(20, 16);
            for (int qp = -48; qp <= 51; qp++)
            {
                blocks.SetQp(4, 4, 12, 8, qp);
                for (int y = 0; y < 16; y++)
                {
                    for (int x = 0; x < 20; x++)
                    {
                        int expected = x >= 4 && x < 16 && y >= 4 && y < 12 ? qp : int.MinValue;
                        Assert.Equal(expected, blocks.Qp(x, y));
                    }
                }
            }

            blocks.SetQp(8, 8, 4, 4, -12);
            Assert.Equal(-12, blocks.Qp(11, 11));
            Assert.Equal(51, blocks.Qp(7, 11));
            Assert.Equal(51, blocks.Qp(11, 7));
            Assert.Equal(51, blocks.Qp(12, 11));
            Assert.Equal(int.MinValue, blocks.Qp(11, 12));
            Assert.Equal(int.MinValue, blocks.Qp(-1, 0));
            Assert.Equal(int.MinValue, blocks.Qp(0, -1));
            Assert.Equal(int.MinValue, blocks.Qp(20, 0));
            Assert.Equal(int.MinValue, blocks.Qp(0, 16));
        }

        [Theory]
        [InlineData(-127)]
        [InlineData(127)]
        public void StorageLimitsRemainDistinctFromUnavailable(int qp)
        {
            using CodingBlockMap blocks = new CodingBlockMap(8, 4);
            blocks.SetQp(0, 0, 4, 4, qp);
            Assert.Equal(qp, blocks.Qp(3, 3));
            Assert.Equal(int.MinValue, blocks.Qp(4, 3));
        }

        [Theory]
        [InlineData(int.MinValue)]
        [InlineData(-129)]
        [InlineData(-128)]
        [InlineData(128)]
        [InlineData(int.MaxValue)]
        public void UnrepresentableQuantizersCannotOverwriteExistingCells(int qp)
        {
            using CodingBlockMap blocks = new CodingBlockMap(8, 8);
            blocks.SetQp(0, 0, 8, 8, 37);
            Assert.Throws<ArgumentOutOfRangeException>(() => blocks.SetQp(0, 0, 8, 8, qp));
            for (int y = 0; y < 8; y++)
            {
                for (int x = 0; x < 8; x++)
                {
                    Assert.Equal(37, blocks.Qp(x, y));
                }
            }
        }
    }
}
