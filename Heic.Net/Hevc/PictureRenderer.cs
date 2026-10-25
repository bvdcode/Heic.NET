// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using System;
using System.IO;
using Heic.Net.Heif;

namespace Heic.Net.Hevc
{
    /// <summary>
    /// Copies the SPS conformance window into owned RGBA8 rows with chroma sampled at the coded resolution.
    /// </summary>
    /// <remarks>
    /// Chroma sample (x/2,y/2) supplies each 2x2 luma block; no smoothing is applied.
    /// Alpha auxiliary samples use their SEI range and clipping operations when present,
    /// otherwise their full integer range, independently of video colour-range flags. Output arrays never alias reconstructed picture planes.
    /// </remarks>
    internal static class PictureRenderer
    {
        public static RgbaImage Render(DecodedPicture picture, ColorEncoding color, ReadOnlyMemory<byte> iccProfile, bool alpha, PixelBufferScope buffers, AlphaChannelInformation? alphaInformation = null)
        {
            SequenceParameters sequence = picture.Sequence;
            if (alpha && alphaInformation is not null && alphaInformation.BitDepth != sequence.BitDepth)
            {
                throw new InvalidDataException("Alpha information depth disagrees with the HEVC sequence.");
            }
            int width = sequence.VisibleWidth;
            int height = sequence.VisibleHeight;
            Memory<byte> pixels = buffers.Allocate(checked(width * height * 4));
            ColorConversion? conversion = null;
            if (!alpha)
            {
                int matrix = picture.Cb is null ? 1 : color.Matrix;
                conversion = new ColorConversion(sequence.BitDepth, color.FullRange, matrix);
            }

            int maximum = (1 << sequence.BitDepth) - 1;
            int subWidth = sequence.SubWidthC;
            int subHeight = sequence.SubHeightC;
            for (int y = 0; y < height; y++)
            {
                int sy = y + sequence.CropTop;
                ReadOnlySpan<ushort> luma = picture.Luma.Row(sy).Slice(sequence.CropLeft, width);
                Span<byte> row = pixels.Span.Slice(y * width * 4, width * 4);
                if (alpha)
                {
                    for (int x = 0; x < width; x++)
                    {
                        byte value = alphaInformation is null
                            ? (byte)((luma[x] * 255 + maximum / 2) / maximum)
                            : alphaInformation.Convert(luma[x]);
                        WriteGray(value, row.Slice(x * 4, 4));
                    }
                }
                else if (picture.Cb is null || picture.Cr is null)
                {
                    for (int x = 0; x < width; x++)
                    {
                        WriteGray(conversion!.Gray(luma[x]), row.Slice(x * 4, 4));
                    }
                }
                else
                {
                    ReadOnlySpan<ushort> cb = picture.Cb.Row(sy / subHeight).Slice(sequence.CropLeft / subWidth);
                    ReadOnlySpan<ushort> cr = picture.Cr.Row(sy / subHeight).Slice(sequence.CropLeft / subWidth);
                    for (int x = 0; x < width; x++)
                    {
                        conversion!.Convert(luma[x], cb[x / subWidth], cr[x / subWidth], row.Slice(x * 4, 4));
                    }
                }
            }

            return new RgbaImage(width, height, pixels, new ImageColorInformation(color.Primaries, color.Transfer, iccProfile));
        }

        private static void WriteGray(byte value, Span<byte> target)
        {
            target[0] = value;
            target[1] = value;
            target[2] = value;
            target[3] = 255;
        }
    }
}
