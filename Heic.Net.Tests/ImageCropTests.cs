// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov (bvdcode) <https://belov.us>

using System;
using System.IO;
using Heic.Net.Heif;
using Xunit;

namespace Heic.Net.Tests
{
    public class ImageCropTests
    {
        [Fact]
        public void LeadingApertureDoesNotReorderOrientations()
        {
            HeifBox[] properties =
            {
                HeifTestFile.CleanAperture(73, 59, 3, 5, 65, 51),
                new HeifBox(HeifBox.Code("irot"), 0, new byte[] { 1 }),
                new HeifBox(HeifBox.Code("clap"), 0, Array.Empty<byte>())
            };
            ImageCrop crop = ImageCrop.ReadPrefix(73, 59, properties);
            Assert.Equal(3, crop.Left);
            Assert.Equal(5, crop.Top);
            Assert.Equal(65, crop.Width);
            Assert.Equal(51, crop.Height);
            Assert.Equal(1, crop.PropertyCount);
            ImageCrop full = ImageCrop.ReadPrefix(73, 59, properties.AsSpan(1).ToArray());
            Assert.Equal(0, full.PropertyCount);
            Assert.Equal(73, full.Width);
            Assert.Equal(59, full.Height);
        }

        [Theory]
        [InlineData(0, 0, 1, 1)]
        [InlineData(0, 0, 73, 59)]
        [InlineData(72, 58, 1, 1)]
        public void CroppedStorageDoesNotAliasItsSource(int left, int top, int width, int height)
        {
            using PixelBufferScope buffers = new PixelBufferScope(false);
            byte[] pixels = new byte[73 * 59 * 4];
            Array.Fill(pixels, (byte)173);
            RgbaImage source = new RgbaImage(73, 59, pixels, new ImageColorInformation(1, 1));
            RgbaImage result = ImageTransforms.Apply(source, HeifTestFile.CleanAperture(73, 59, left, top, width, height), buffers);
            Array.Clear(pixels);
            Assert.All(result.Pixels.ToArray(), value => Assert.Equal((byte)173, value));
        }

        [Theory]
        [InlineData(-1, 0, 10, 10)]
        [InlineData(0, -1, 10, 10)]
        [InlineData(1, 0, 73, 59)]
        [InlineData(0, 1, 73, 59)]
        [InlineData(0, 0, 74, 59)]
        [InlineData(0, 0, 73, 60)]
        [InlineData(0, 0, 0, 59)]
        public void AperturesOutsideTheImageAreRejected(int left, int top, int width, int height)
        {
            HeifBox property = HeifTestFile.CleanAperture(73, 59, left, top, width, height);
            Assert.Throws<InvalidDataException>(() => ImageCrop.Read(73, 59, property));
        }

        [Fact]
        public void InvalidRationalsAndTrailingDataAreRejected()
        {
            byte[] payload = HeifTestFile.CleanAperture(73, 59, 1, 1, 65, 51).Payload.ToArray();
            foreach (int offset in new[] { 4, 12, 20, 28 })
            {
                byte[] invalid = (byte[])payload.Clone();
                Array.Clear(invalid, offset, 4);
                Assert.Throws<InvalidDataException>(() => Read(invalid));
            }
            for (int length = 0; length < payload.Length; length++)
            {
                byte[] truncated = payload.AsSpan(0, length).ToArray();
                Assert.Throws<InvalidDataException>(() => Read(truncated));
            }
            Assert.Throws<InvalidDataException>(() => Read(HeifTestFile.Join(payload, new byte[1])));
            byte[] fraction = (byte[])payload.Clone();
            fraction[7] = 2;
            Assert.Throws<UnsupportedHeicFeatureException>(() => Read(fraction));
            fraction = (byte[])payload.Clone();
            fraction[19]++;
            Assert.Throws<UnsupportedHeicFeatureException>(() => Read(fraction));
            Assert.Throws<InvalidDataException>(() => ImageCrop.Read(73, 59,
                new HeifBox(HeifBox.Code("irot"), 0, new byte[] { 0 })));
        }

        private static ImageCrop Read(byte[] payload) => ImageCrop.Read(73, 59, new HeifBox(HeifBox.Code("clap"), 0, payload));
    }
}
