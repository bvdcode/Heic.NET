// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using System;
using System.Linq;
using Heic.Net.Hevc;
using Xunit;

namespace Heic.Net.Tests
{
    public class ScalingListsTests
    {
        [Fact]
        public void ExplicitMatricesCopyPredictionAndExpandTheirBaseCoordinates()
        {
            TestBitWriter writer = new TestBitWriter();
            for (int sizeId = 0; sizeId < 4; sizeId++)
            {
                int step = sizeId == 3 ? 3 : 1;
                for (int matrixId = 0; matrixId < 6; matrixId += step)
                {
                    writer.Flag(matrixId == 0);
                    if (matrixId == 0)
                    {
                        if (sizeId > 1)
                        {
                            writer.Se(15);
                        }
                        for (int i = 0; i < (sizeId == 0 ? 16 : 64); i++)
                        {
                            writer.Se(1);
                        }
                    }
                    else
                    {
                        writer.Ue(matrixId == step ? 1u : 0u);
                    }
                }
            }

            ScalingLists lists = ScalingLists.Read(new BitReader(writer.ToArray()));
            for (int sizeId = 0; sizeId < 4; sizeId++)
            {
                int baseSize = sizeId == 0 ? 4 : 8;
                var coordinates = from y in Enumerable.Range(0, baseSize)
                                  from x in Enumerable.Range(0, baseSize)
                                  orderby x + y, x
                                  select (X: x, Y: y);
                int scanPosition = 0;
                foreach ((int x, int y) in coordinates)
                {
                    int factor = (sizeId > 1 ? 24 : 9) + scanPosition++;
                    int scale = 1 << Math.Max(0, sizeId - 1);
                    for (int dy = 0; dy < scale; dy++)
                    {
                        for (int dx = 0; dx < scale; dx++)
                        {
                            int px = x * scale + dx;
                            int py = y * scale + dy;
                            int expected = sizeId > 1 && px == 0 && py == 0 ? 23 : factor;
                            Assert.Equal(expected, lists.Factor(sizeId + 2, 0, px, py));
                            Assert.Equal(expected, lists.Factor(sizeId + 2, sizeId == 3 ? 3 : 1, px, py));
                        }
                    }
                }
            }
        }

        [Fact]
        public void FullResolutionChromaUsesExpandedSixteenPointScalingLists()
        {
            TestBitWriter writer = new TestBitWriter();
            for (int sizeId = 0; sizeId < 4; sizeId++)
            {
                for (int matrixId = 0; matrixId < 6; matrixId += sizeId == 3 ? 3 : 1)
                {
                    writer.Flag(true);
                    if (sizeId > 1)
                    {
                        writer.Se(10 * sizeId + matrixId);
                    }
                    for (int i = 0; i < (sizeId == 0 ? 16 : 64); i++)
                    {
                        writer.Se(1);
                    }
                }
            }
            ScalingLists lists = ScalingLists.Read(new BitReader(writer.ToArray()));
            foreach (int matrixId in new[] { 1, 2, 4, 5 })
            {
                for (int y = 0; y < 32; y++)
                {
                    for (int x = 0; x < 32; x++)
                    {
                        int expected = x == 0 && y == 0 ? 28 + matrixId : 29 + matrixId + DiagonalIndex(x / 4, y / 4);
                        Assert.Equal(expected, lists.Factor(5, matrixId, x, y));
                    }
                }
            }
        }

        private static int DiagonalIndex(int x, int y)
        {
            int index = 0;
            for (int sum = 0; sum <= 14; sum++)
            {
                for (int px = 0; px < 8; px++)
                {
                    int py = sum - px;
                    if (py >= 0 && py < 8)
                    {
                        if (px == x && py == y)
                        {
                            return index;
                        }
                        index++;
                    }
                }
            }
            throw new ArgumentOutOfRangeException(nameof(x));
        }

        [Fact]
        public void DisabledListsAndDefaultListsHaveDifferentHighFrequencyWeights()
        {
            Assert.Equal(16, ScalingLists.Flat.Factor(5, 0, 31, 31));
            Assert.Equal(115, ScalingLists.Default.Factor(5, 0, 31, 31));
            Assert.Equal(91, ScalingLists.Default.Factor(5, 3, 31, 31));
            Assert.Equal(16, ScalingLists.Default.Factor(5, 0, 0, 0));
        }

        [Theory]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        public void BlockScansArePermutationsOfGroupedCoordinateOrder(int log2Size)
        {
            int size = 1 << log2Size;
            for (int scan = 0; scan < 3; scan++)
            {
                int[] actual = CoefficientScan.Block(log2Size, scan).ToArray();
                Assert.Equal(Enumerable.Range(0, size * size), actual.Order());
                for (int group = 0; group < size * size / 16; group++)
                {
                    int groupX = actual[group * 16] % size / 4;
                    int groupY = actual[group * 16] / size / 4;
                    Assert.All(actual.Skip(group * 16).Take(16), offset =>
                    {
                        Assert.Equal(groupX, offset % size / 4);
                        Assert.Equal(groupY, offset / size / 4);
                    });
                }
            }

            Assert.Equal(new[] { 0, size, 1, 2 * size, size + 1, 2 }, CoefficientScan.Block(log2Size, 0).Slice(0, 6).ToArray());
        }
    }
}
