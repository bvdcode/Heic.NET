// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using System;
using Heic.Net.Heif;

namespace Heic.Net.Hevc
{
    /// <summary>
    /// Copies the SPS conformance window into owned RGBA8 rows with block-replicated 4:2:0 chroma.
    /// </summary>
    /// <remarks>
    /// Chroma sample (x/2,y/2) supplies each 2x2 luma block; no smoothing is applied.
    /// Alpha auxiliary samples are normalized over their full integer range independently
    /// of video colour-range flags. Output arrays never alias reconstructed picture planes.
    /// </remarks>
    internal static class PictureRenderer
    {
        public static RgbaImage Render(DecodedPicture picture, ColorEncoding color, ReadOnlyMemory<byte> iccProfile, bool alpha)
        {
            SequenceParameters sequence = picture.Sequence;
            int width = sequence.VisibleWidth;
            int height = sequence.VisibleHeight;
            byte[] pixels = new byte[checked(width * height * 4)];
            ColorConversion? conversion = null;
            if (!alpha)
            {
                int matrix = picture.Cb is null ? 1 : color.Matrix;
                conversion = new ColorConversion(sequence.BitDepth, color.FullRange, matrix);
            }

            for (int y = 0; y < height; y++)
            {
                int sy = y + sequence.CropTop;
                ReadOnlySpan<ushort> luma = picture.Luma.Row(sy);
                for (int x = 0; x < width; x++)
                {
                    int sx = x + sequence.CropLeft;
                    Span<byte> target = pixels.AsSpan((y * width + x) * 4, 4);
                    if (alpha || picture.Cb is null || picture.Cr is null)
                    {
                        int maximum = (1 << sequence.BitDepth) - 1;
                        byte value = alpha ? (byte)((luma[sx] * 255 + maximum / 2) / maximum) : conversion!.Gray(luma[sx]);
                        target[0] = value;
                        target[1] = value;
                        target[2] = value;
                        target[3] = 255;
                    }
                    else
                    {
                        conversion!.Convert(luma[sx], picture.Cb.Row(sy / 2)[sx / 2], picture.Cr.Row(sy / 2)[sx / 2], target);
                    }
                }
            }

            return new RgbaImage(width, height, pixels, new ImageColorInformation(color.Primaries, color.Transfer, iccProfile));
        }
    }
}
