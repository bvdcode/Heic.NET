// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov (bvdcode) <https://belov.us>

using System;
using Xunit;

namespace Heic.Net.Tests
{
    public class ImageOwnershipTests
    {
        [Fact]
        public void CopyDoesNotShareCallerStorage()
        {
            RgbaImage image = new RgbaImage(1, 1, new byte[] { 1, 2, 3, 255 }, new ImageColorInformation(1, 1));
            byte[] copy = new byte[4];
            image.CopyTo(copy);
            copy[0] = 99;
            Assert.Equal(1, image.Pixels.Span[0]);
            Assert.Equal(4, image.Stride);
        }

        [Fact]
        public void InvalidDimensionsAreRejected()
        {
            Assert.Throws<ArgumentException>(() => new RgbaImage(2, 1, new byte[4], new ImageColorInformation(1, 1)));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HeicDecodeOptions(maxPixels: 0));
        }
    }
}
