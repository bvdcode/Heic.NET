// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using System;
using Xunit;

namespace Heic.Net.Tests
{
    public class PixelBufferScopeTests
    {
        [Fact]
        public void TemporaryBuffersExposeOnlyTheirLogicalLengthAndRemainDistinct()
        {
            PixelBufferScope buffers = new PixelBufferScope(true);
            Memory<byte> first = buffers.Allocate(24);
            first.Span.Fill(37);
            Memory<byte> second = buffers.Allocate(24);
            second.Span.Fill(91);
            Assert.Equal(24, first.Length);
            Assert.Equal(24, second.Length);
            Assert.Equal(37, first.Span[0]);
            buffers.Dispose();
            buffers.Dispose();
            Assert.Throws<ObjectDisposedException>(() => buffers.Allocate(24));
        }

        [Fact]
        public void OwnedImageSurvivesScopeDisposalAndLaterRentals()
        {
            RgbaImage image;
            using (PixelBufferScope buffers = new PixelBufferScope(false))
            {
                Memory<byte> pixels = buffers.Allocate(24);
                pixels.Span.Fill(53);
                image = new RgbaImage(3, 2, pixels, new ImageColorInformation(1, 1));
            }

            using PixelBufferScope temporary = new PixelBufferScope(true);
            temporary.Allocate(24).Span.Fill(97);
            Assert.Equal(24, image.Pixels.Length);
            foreach (byte value in image.Pixels.Span)
            {
                Assert.Equal(53, value);
            }
        }
    }
}
