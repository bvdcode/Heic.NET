// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov (bvdcode) <https://belov.us>

using System;
using Heic.Net.Heif;
using Heic.Net.Hevc;
using Xunit;

namespace Heic.Net.Tests
{
    public class CropRenderingTests
    {
        [Theory]
        [InlineData("pattern-8bit.hevc")]
        [InlineData("pattern-10bit.hevc")]
        [InlineData("pattern-mono.hevc")]
        [InlineData("wpp-chroma422-8bit.hevc")]
        [InlineData("wpp-chroma422-10bit.hevc")]
        [InlineData("wpp-chroma444-8bit.hevc")]
        [InlineData("wpp-chroma444-10bit.hevc")]
        public void ApertureRenderingMatchesCroppingTheCompleteImage(string file)
        {
            using DecodedPicture picture = new IntraPictureDecoder(IntraPictureFixture.ReadSlice(file)).Decode();
            using PixelBufferScope buffers = new PixelBufferScope(false);
            SequenceParameters sequence = picture.Sequence;
            ColorEncoding color = ColorEncoding.FromVideo(sequence.Video);
            ImageCrop full = ImageCrop.ReadPrefix(sequence.VisibleWidth, sequence.VisibleHeight, []);
            byte[] icc = { 1, 3, 7, 11 };
            foreach (bool alpha in new[] { false, true })
            {
                foreach (AlphaChannelInformation? information in new AlphaChannelInformation?[]
                {
                    null, new AlphaChannelInformation(sequence.BitDepth, 17, (1 << sequence.BitDepth) - 18, true, true, true)
                })
                {
                    RgbaImage source = PictureRenderer.Render(picture, full, color, icc, alpha, buffers, information);
                    byte[] original = source.Pixels.ToArray();
                    foreach ((int left, int top, int width, int height) in new[]
                    {
                        (0, 0, full.Width, full.Height),
                        (1, 1, Math.Min(65, full.Width - 1), Math.Min(17, full.Height - 1)),
                        (1, 0, 1, full.Height),
                        (0, 1, full.Width, 1),
                        (full.Width - 1, full.Height - 1, 1, 1)
                    })
                    {
                        HeifBox property = HeifTestFile.CleanAperture(full.Width, full.Height, left, top, width, height);
                        ImageCrop crop = ImageCrop.Read(full.Width, full.Height, property);
                        RgbaImage expected = ImageTransforms.Apply(source, property, buffers);
                        RgbaImage actual = PictureRenderer.Render(picture, crop, color, icc, alpha, buffers, information);
                        Assert.Equal(expected.Width, actual.Width);
                        Assert.Equal(expected.Height, actual.Height);
                        Assert.Equal(expected.Pixels.ToArray(), actual.Pixels.ToArray());
                        Assert.True(source.ColorInformation.Matches(actual.ColorInformation));
                    }
                    ToggleFirst(picture.Luma);
                    Assert.Equal(original, source.Pixels.ToArray());
                    ToggleFirst(picture.Luma);
                }
            }
        }
        private static void ToggleFirst(SamplePlane plane)
        {
            switch (plane.BitDepth)
            {
                case 8:
                    plane.ByteView.Row(0)[0] ^= 255;
                    break;
                case 10:
                    plane.WordView.Row(0)[0] ^= 255;
                    break;
                default:
                    throw new InvalidOperationException();
            }
        }
    }
}
