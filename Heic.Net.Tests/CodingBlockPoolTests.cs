// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using System;
using Heic.Net.Hevc;
using Xunit;

namespace Heic.Net.Tests
{
    public class CodingBlockPoolTests
    {
        [Fact]
        public void ReusedMetadataIsInitializedWithoutChangingRentedPadding()
        {
            TrackingArrayPool<byte> metadata = new TrackingArrayPool<byte>(167);
            TrackingArrayPool<sbyte> quantizers = new TrackingArrayPool<sbyte>(-77);
            TrackingArrayPool<bool> bypass = new TrackingArrayPool<bool>(true);
            foreach (int size in new[] { 32, 8, 16, 4, 32 })
            {
                using (CodingBlockMap map = new CodingBlockMap(size, size, metadata, quantizers, bypass))
                {
                    AssertEmpty(map, size);
                    map.SetCodingUnit(0, 0, size, 2, true);
                    map.SetMode(0, 0, size, 34);
                    map.SetQp(0, 0, size, size, -12);
                    Assert.Equal(2, map.Depth(size - 1, size - 1));
                    Assert.Equal(34, map.Mode(size - 1, size - 1));
                    Assert.Equal(-12, map.Qp(size - 1, size - 1));
                    Assert.True(map.Bypass(size - 1, size - 1));
                    Assert.True(map.Boundary(0, 0, true));
                    Assert.True(map.Boundary(0, 0, false));
                    int count = (size * size) / 16;
                    foreach (byte[] buffer in metadata.Buffers)
                    {
                        Assert.All(buffer.AsSpan(count).ToArray(), value => Assert.Equal(167, value));
                    }
                    foreach (sbyte[] buffer in quantizers.Buffers)
                    {
                        Assert.All(buffer.AsSpan(count).ToArray(), value => Assert.Equal(-77, value));
                    }
                    foreach (bool[] buffer in bypass.Buffers)
                    {
                        Assert.All(buffer.AsSpan(count).ToArray(), value => Assert.True(value));
                    }
                }
                Assert.Equal(0, metadata.Outstanding);
                Assert.Equal(0, quantizers.Outstanding);
                Assert.Equal(0, bypass.Outstanding);
            }
            Assert.Equal(3, metadata.Buffers.Count);
            Assert.Single(quantizers.Buffers);
            Assert.Single(bypass.Buffers);
        }

        [Fact]
        public void ConcurrentlyOwnedMapsDoNotShareRentalsAndDisposedMapsCannotModifyReusedBuffers()
        {
            TrackingArrayPool<byte> metadata = new TrackingArrayPool<byte>(167);
            TrackingArrayPool<sbyte> quantizers = new TrackingArrayPool<sbyte>(-77);
            TrackingArrayPool<bool> bypass = new TrackingArrayPool<bool>(true);
            CodingBlockMap first = new CodingBlockMap(8, 8, metadata, quantizers, bypass);
            using CodingBlockMap second = new CodingBlockMap(8, 8, metadata, quantizers, bypass);
            first.SetQp(0, 0, 8, 8, 51);
            Assert.Equal(int.MinValue, second.Qp(0, 0));
            first.Dispose();
            first.Dispose();
            Assert.Equal(3, metadata.Returns);
            Assert.Equal(1, quantizers.Returns);
            Assert.Equal(1, bypass.Returns);
            using CodingBlockMap third = new CodingBlockMap(8, 8, metadata, quantizers, bypass);
            AssertEmpty(third, 8);
            Assert.Throws<ObjectDisposedException>(() => first.Mode(0, 0));
            Assert.Throws<ObjectDisposedException>(() => first.Depth(0, 0));
            Assert.Throws<ObjectDisposedException>(() => first.Qp(0, 0));
            Assert.Throws<ObjectDisposedException>(() => first.Bypass(0, 0));
            Assert.Throws<ObjectDisposedException>(() => first.Boundary(0, 0, true));
            Assert.Throws<ObjectDisposedException>(() => first.SetCodingUnit(0, 0, 8, 0, true));
            Assert.Throws<ObjectDisposedException>(() => first.SetMode(0, 0, 8, 3));
            Assert.Throws<ObjectDisposedException>(() => first.SetQp(0, 0, 8, 8, 1));
            Assert.Throws<ObjectDisposedException>(() => first.MarkTransform(0, 0, 8));
            AssertEmpty(third, 8);
            AssertEmpty(second, 8);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        public void FailedConstructionReturnsEveryAcquiredArray(int failedRental)
        {
            TrackingArrayPool<byte> metadata = new TrackingArrayPool<byte>(167);
            TrackingArrayPool<sbyte> quantizers = new TrackingArrayPool<sbyte>(-77);
            TrackingArrayPool<bool> bypass = new TrackingArrayPool<bool>(true);
            switch (failedRental)
            {
                case 0:
                    metadata.FailOnRent = 1;
                    break;
                case 1:
                    metadata.FailOnRent = 2;
                    break;
                case 2:
                    quantizers.FailOnRent = 1;
                    break;
                case 3:
                    metadata.FailOnRent = 3;
                    break;
                case 4:
                    bypass.FailOnRent = 1;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(failedRental));
            }
            Assert.Throws<InvalidOperationException>(() => new CodingBlockMap(32, 32, metadata, quantizers, bypass));
            Assert.Equal(0, metadata.Outstanding);
            Assert.Equal(0, quantizers.Outstanding);
            Assert.Equal(0, bypass.Outstanding);
            Assert.Equal(failedRental, metadata.Returns + quantizers.Returns + bypass.Returns);
        }

        private static void AssertEmpty(CodingBlockMap map, int size)
        {
            for (int y = 0; y < size; y += 4)
            {
                for (int x = 0; x < size; x += 4)
                {
                    Assert.Equal(1, map.Mode(x, y));
                    Assert.Equal(-1, map.Depth(x, y));
                    Assert.Equal(int.MinValue, map.Qp(x, y));
                    Assert.False(map.Bypass(x, y));
                    Assert.False(map.Boundary(x, y, true));
                    Assert.False(map.Boundary(x, y, false));
                }
            }
        }
    }
}
