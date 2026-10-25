// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using System;
using System.IO;
using Heic.Net.Binary;

namespace Heic.Net.Heif
{
    /// <summary>
    /// Applies transformative properties in their item association order.
    /// </summary>
    /// <remarks>
    /// Rotations are counterclockwise quarter turns. HEIF 2022 mirror axis 0 reverses
    /// rows, and axis 1 reverses columns. Clean-aperture offsets locate its centre relative
    /// to the stored image centre; only exact integral sample rectangles are supported.
    /// Each result owns its pixels and never modifies the source image.
    /// </remarks>
    internal static class ImageTransforms
    {
        public static RgbaImage Apply(RgbaImage image, HeifBox property)
        {
            ByteReader reader = new ByteReader(property.Payload);
            RgbaImage result;
            if (property.Type == HeifBox.Code("irot"))
            {
                int turns = reader.U8();
                if (turns > 3)
                {
                    throw new InvalidDataException("Nonzero image rotation reserved bits.");
                }

                result = Orient(image, turns, null);
            }
            else if (property.Type == HeifBox.Code("imir"))
            {
                int axis = reader.U8();
                if (axis > 1)
                {
                    throw new InvalidDataException("Nonzero image mirror reserved bits.");
                }

                result = Orient(image, 0, axis);
            }
            else if (property.Type == HeifBox.Code("clap"))
            {
                int width = Dimension(reader, image.Width);
                int height = Dimension(reader, image.Height);
                long left = Origin(reader, image.Width, width);
                long top = Origin(reader, image.Height, height);
                if (width <= 0 || height <= 0 || left < 0 || top < 0 || left + width > image.Width || top + height > image.Height)
                {
                    throw new InvalidDataException("Clean aperture exceeds the image.");
                }

                result = Crop(image, (int)left, (int)top, width, height);
            }
            else
            {
                throw new InvalidDataException("Expected an image transformation.");
            }

            if (reader.Remaining != 0)
            {
                throw new InvalidDataException("Unexpected image transformation data.");
            }

            return result;
        }

        public static RgbaImage Orient(RgbaImage source, int turns, int? mirrorAxis)
        {
            int width = (turns & 1) == 0 ? source.Width : source.Height;
            int height = (turns & 1) == 0 ? source.Height : source.Width;
            byte[] pixels = new byte[checked(width * height * 4)];
            for (int y = 0; y < source.Height; y++)
            {
                for (int x = 0; x < source.Width; x++)
                {
                    int tx;
                    int ty;
                    switch (turns)
                    {
                        case 0:
                            tx = x;
                            ty = y;
                            break;
                        case 1:
                            tx = y;
                            ty = source.Width - 1 - x;
                            break;
                        case 2:
                            tx = source.Width - 1 - x;
                            ty = source.Height - 1 - y;
                            break;
                        case 3:
                            tx = source.Height - 1 - y;
                            ty = x;
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(turns));
                    }

                    switch (mirrorAxis)
                    {
                        case null:
                            break;
                        case 0:
                            ty = height - 1 - ty;
                            break;
                        case 1:
                            tx = width - 1 - tx;
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(mirrorAxis));
                    }

                    source.Pixels.Span.Slice((y * source.Width + x) * 4, 4).CopyTo(pixels.AsSpan((ty * width + tx) * 4, 4));
                }
            }

            return new RgbaImage(width, height, pixels, source.ColorInformation);
        }

        private static RgbaImage Crop(RgbaImage source, int left, int top, int width, int height)
        {
            byte[] pixels = new byte[checked(width * height * 4)];
            for (int y = 0; y < height; y++)
            {
                source.Pixels.Span.Slice((top + y) * source.Stride + left * 4, width * 4)
                    .CopyTo(pixels.AsSpan(y * width * 4, width * 4));
            }

            return new RgbaImage(width, height, pixels, source.ColorInformation);
        }

        private static int Dimension(ByteReader reader, int extent)
        {
            uint numerator = reader.U32();
            uint denominator = reader.U32();
            if (denominator == 0)
            {
                throw new InvalidDataException("Clean aperture denominator must be nonzero.");
            }

            if (numerator % denominator != 0)
            {
                throw new UnsupportedHeicFeatureException("fractional clean aperture dimensions");
            }

            uint result = numerator / denominator;
            if (result == 0 || result > extent)
            {
                throw new InvalidDataException("Clean aperture exceeds the image.");
            }

            return (int)result;
        }

        private static long Origin(ByteReader reader, int extent, int croppedExtent)
        {
            int offset = unchecked((int)reader.U32());
            uint denominator = reader.U32();
            if (denominator == 0)
            {
                throw new InvalidDataException("Clean aperture denominator must be nonzero.");
            }

            long numerator = (long)(extent - croppedExtent) * denominator + 2L * offset;
            long divisor = 2L * denominator;
            if (numerator % divisor != 0)
            {
                throw new UnsupportedHeicFeatureException("fractional clean aperture offsets");
            }

            return numerator / divisor;
        }
    }
}
