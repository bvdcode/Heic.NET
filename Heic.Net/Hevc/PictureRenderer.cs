// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using System;
using System.IO;
using Heic.Net.Heif;

namespace Heic.Net.Hevc
{
    /// <summary>
    /// Renders a clean aperture within the SPS conformance window into owned RGBA8 rows.
    /// </summary>
    /// <remarks>
    /// Chroma sample (x/2,y/2) supplies each 2x2 luma block; no smoothing is applied.
    /// Alpha auxiliary samples use their SEI range and clipping operations when present,
    /// otherwise their full integer range, independently of video colour-range flags. Output arrays never alias reconstructed picture planes.
    /// </remarks>
    internal static class PictureRenderer
    {
        public static RgbaImage Render(DecodedPicture picture, ImageCrop crop, ColorEncoding color, ReadOnlyMemory<byte> iccProfile, bool alpha, PixelBufferScope buffers, AlphaChannelInformation? alphaInformation = null)
        {
            SequenceParameters sequence = picture.Sequence;
            if (alpha && alphaInformation is not null && alphaInformation.BitDepth != sequence.BitDepth)
            {
                throw new InvalidDataException("Alpha information depth disagrees with the HEVC sequence.");
            }
            int width = crop.Width;
            int height = crop.Height;
            int left = sequence.CropLeft + crop.Left;
            int top = sequence.CropTop + crop.Top;
            Memory<byte> pixels = buffers.Allocate(checked(width * height * 4));
            ColorConversion? conversion = null;
            if (!alpha)
            {
                int matrix = picture.Cb is null ? 1 : color.Matrix;
                conversion = new ColorConversion(sequence.BitDepth, color.FullRange, matrix);
            }

            if (!alpha && picture.Cb is not null && picture.Cr is not null)
            {
                int subWidth = sequence.SubWidthC;
                int subHeight = sequence.SubHeightC;
                ColorPlaneLayout layout = new ColorPlaneLayout(width, height, picture.Luma.Stride,
                    picture.Cb.Stride, subWidth, subHeight, left % subWidth, top % subHeight);
                int lumaOffset = top * picture.Luma.Stride + left;
                int chromaOffset = top / subHeight * picture.Cb.Stride + left / subWidth;
                conversion!.ConvertRows(picture.Luma.Samples.Span.Slice(lumaOffset),
                    picture.Cb.Samples.Span.Slice(chromaOffset), picture.Cr.Samples.Span.Slice(chromaOffset), layout, pixels.Span);
            }
            else
            {
                int maximum = (1 << sequence.BitDepth) - 1;
                for (int y = 0; y < height; y++)
                {
                    int sy = y + top;
                    ReadOnlySpan<ushort> luma = picture.Luma.Row(sy).Slice(left, width);
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
                    else
                    {
                        for (int x = 0; x < width; x++)
                        {
                            WriteGray(conversion!.Gray(luma[x]), row.Slice(x * 4, 4));
                        }
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
