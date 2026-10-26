// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using System;
using Heic.Net.Hevc;
using Xunit;

namespace Heic.Net.Tests
{
    public class PlaneStorageTests
    {
        [Theory]
        [InlineData(8)]
        [InlineData(10)]
        public void RentalsExposeOnlyTheirFormatAndResetLogicalSamples(int depth)
        {
            SamplePlane plane = new SamplePlane(12, 12, depth);
            Fill(plane, 173);
            if (depth == 8)
            {
                Assert.Equal(144, plane.ByteView.Samples.Length);
                Assert.Throws<InvalidOperationException>(() => plane.WordView.Width);
                Assert.Throws<ArgumentOutOfRangeException>(() => plane.ByteView.Row(12).Length);
            }
            else
            {
                Assert.Equal(144, plane.WordView.Samples.Length);
                Assert.Throws<InvalidOperationException>(() => plane.ByteView.Width);
                Assert.Throws<ArgumentOutOfRangeException>(() => plane.WordView.Row(12).Length);
            }
            plane.Dispose();
            plane.Dispose();
            Assert.Throws<ObjectDisposedException>(() => plane.ByteView.Width);
            Assert.Throws<ObjectDisposedException>(() => plane.WordView.Width);
            Assert.Throws<ObjectDisposedException>(() => plane.Store(0, 0, 4, new int[16], new int[16]));
            using SamplePlane next = new SamplePlane(12, 12, depth);
            for (int y = 0; y < 12; y++)
            {
                for (int x = 0; x < 12; x++)
                {
                    Assert.Equal(0, Read(next, x, y));
                    Assert.Equal(-1, next.Neighbour(x, y));
                }
            }
        }

        [Theory]
        [InlineData(8)]
        [InlineData(10)]
        public void StoredBlocksClipAtTheirDepthWithoutChangingSurroundingSamples(int depth)
        {
            using SamplePlane plane = new SamplePlane(12, 12, depth);
            Fill(plane, 173);
            int maximum = (1 << depth) - 1;
            int[] prediction = new int[16];
            int[] residual = new int[16];
            for (int i = 0; i < 16; i++)
            {
                prediction[i] = (i * maximum) / 15;
                residual[i] = (i % 2) == 0 ? -100 : 100;
            }
            plane.Store(4, 4, 4, prediction, residual);
            for (int y = 0; y < 12; y++)
            {
                for (int x = 0; x < 12; x++)
                {
                    if ((x >= 4) && (x < 8) && (y >= 4) && (y < 8))
                    {
                        int index = (((y - 4) * 4) + x) - 4;
                        int expected = Math.Clamp(prediction[index] + residual[index], 0, maximum);
                        Assert.Equal(expected, Read(plane, x, y));
                        Assert.Equal(expected, plane.Neighbour(x, y));
                    }
                    else
                    {
                        Assert.Equal(173, Read(plane, x, y));
                        Assert.Equal(-1, plane.Neighbour(x, y));
                    }
                }
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(9)]
        [InlineData(12)]
        [InlineData(16)]
        public void InvalidSampleDepthIsRejected(int depth)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SamplePlane(8, 8, depth));
        }

        private static int Read(SamplePlane plane, int x, int y)
        {
            switch (plane.BitDepth)
            {
                case 8:
                    return plane.ByteView.Row(y)[x];
                case 10:
                    return plane.WordView.Row(y)[x];
                default:
                    throw new InvalidOperationException();
            }
        }

        private static void Fill(SamplePlane plane, int value)
        {
            switch (plane.BitDepth)
            {
                case 8:
                    plane.ByteView.Samples.Fill((byte)value);
                    break;
                case 10:
                    plane.WordView.Samples.Fill((ushort)value);
                    break;
                default:
                    throw new InvalidOperationException();
            }
        }
    }
}
