// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov (bvdcode) <https://belov.us>

using System;
using System.IO;
using Heic.Net.Heif;
using Xunit;

namespace Heic.Net.Tests
{
    public class ImageOrientationTests
    {
        [Theory]
        [InlineData(0, null)]
        [InlineData(1, null)]
        [InlineData(2, null)]
        [InlineData(3, null)]
        [InlineData(0, 0)]
        [InlineData(1, 0)]
        [InlineData(2, 0)]
        [InlineData(3, 0)]
        [InlineData(0, 1)]
        [InlineData(1, 1)]
        [InlineData(2, 1)]
        [InlineData(3, 1)]
        public void PartialTilesMatchIndependentCoordinateMapping(int turns, int? axis)
        {
            RgbaImage source = Source(73, 59);
            ImageOrientation orientation = ImageOrientation.Create(source.Width, source.Height, turns, axis);
            RgbaImage actual = CopyTiles(source, orientation);
            RgbaImage expected = Reference(source, turns, axis);
            Assert.Equal(expected.Width, actual.Width);
            Assert.Equal(expected.Height, actual.Height);
            Assert.Equal(expected.Pixels.ToArray(), actual.Pixels.ToArray());
        }

        [Fact]
        public void OrientationPrefixPreservesPropertyOrderAndStopsAtCrop()
        {
            RgbaImage source = Source(73, 59);
            HeifBox[] properties =
            {
                new HeifBox(HeifBox.Code("irot"), 0, new byte[] { 1 }),
                new HeifBox(HeifBox.Code("imir"), 0, new byte[] { 1 }),
                new HeifBox(HeifBox.Code("irot"), 0, new byte[] { 2 }),
                new HeifBox(HeifBox.Code("clap"), 0, Array.Empty<byte>()),
                new HeifBox(HeifBox.Code("irot"), 0, new byte[] { 3 })
            };
            ImageOrientation orientation = ImageOrientation.ReadPrefix(source.Width, source.Height, properties);
            RgbaImage expected = Reference(Reference(Reference(source, 1, null), 0, 1), 2, null);
            Assert.Equal(3, orientation.PropertyCount);
            Assert.Equal(expected.Pixels.ToArray(), CopyTiles(source, orientation).Pixels.ToArray());
        }

        [Theory]
        [InlineData("irot", new byte[] { 4 })]
        [InlineData("imir", new byte[] { 2 })]
        [InlineData("irot", new byte[] { 0, 0 })]
        [InlineData("imir", new byte[] { 1, 0 })]
        public void InvalidOrientationPropertiesAreRejected(string type, byte[] payload)
        {
            HeifBox property = new HeifBox(HeifBox.Code(type), 0, payload);
            Assert.Throws<InvalidDataException>(() => ImageOrientation.Read(4, 8, property));
        }

        private static RgbaImage CopyTiles(RgbaImage source, ImageOrientation orientation)
        {
            const int TileWidth = 40;
            const int TileHeight = 36;
            byte[] pixels = new byte[orientation.Width * orientation.Height * 4];
            for (int top = 0; top < source.Height; top += TileHeight)
            {
                for (int left = 0; left < source.Width; left += TileWidth)
                {
                    byte[] tilePixels = new byte[TileWidth * TileHeight * 4];
                    tilePixels.AsSpan().Fill(173);
                    int width = Math.Min(TileWidth, source.Width - left);
                    int height = Math.Min(TileHeight, source.Height - top);
                    for (int y = 0; y < height; y++)
                    {
                        source.Pixels.Span.Slice(((top + y) * source.Stride) + (left * 4), width * 4)
                            .CopyTo(tilePixels.AsSpan(y * TileWidth * 4, width * 4));
                    }
                    RgbaImage tile = new RgbaImage(TileWidth, TileHeight, tilePixels, source.ColorInformation);
                    orientation.CopyTile(tile, pixels, left, top, width, height);
                }
            }
            return new RgbaImage(orientation.Width, orientation.Height, pixels, source.ColorInformation);
        }

        private static RgbaImage Reference(RgbaImage source, int turns, int? axis)
        {
            int width = (turns % 2) == 0 ? source.Width : source.Height;
            int height = (turns % 2) == 0 ? source.Height : source.Width;
            byte[] pixels = new byte[width * height * 4];
            for (int y = 0; y < source.Height; y++)
            {
                for (int x = 0; x < source.Width; x++)
                {
                    (int tx, int ty) = turns switch
                    {
                        0 => (x, y),
                        1 => (y, source.Width - 1 - x),
                        2 => (source.Width - 1 - x, source.Height - 1 - y),
                        3 => (source.Height - 1 - y, x),
                        _ => throw new ArgumentOutOfRangeException(nameof(turns))
                    };
                    if (axis == 0)
                    {
                        ty = height - 1 - ty;
                    }
                    else if (axis == 1)
                    {
                        tx = width - 1 - tx;
                    }
                    source.Pixels.Span.Slice(((y * source.Width) + x) * 4, 4)
                        .CopyTo(pixels.AsSpan(((ty * width) + tx) * 4, 4));
                }
            }
            return new RgbaImage(width, height, pixels, source.ColorInformation);
        }

        private static RgbaImage Source(int width, int height)
        {
            byte[] pixels = new byte[width * height * 4];
            new Random(193).NextBytes(pixels);
            return new RgbaImage(width, height, pixels, new ImageColorInformation(1, 1));
        }
    }
}
