// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Heic.Net.Binary;

namespace Heic.Net.Heif
{
    internal static class GridImageDecoder
    {
        public static RgbaImage Decode(HeifContainer container, HeifItem item, ImageProperties properties,
            Func<uint, RgbaImage> decode, bool parallel)
        {
            ByteReader reader = new ByteReader(container.ReadData(item.Id));
            if (reader.U8() != 0)
            {
                throw new UnsupportedHeicFeatureException("image grid version");
            }

            byte flags = reader.U8();
            if (flags > 1)
            {
                throw new UnsupportedHeicFeatureException("image grid flags");
            }

            int rows = reader.U8() + 1;
            int columns = reader.U8() + 1;
            uint width = flags == 0 ? reader.U16() : reader.U32();
            uint height = flags == 0 ? reader.U16() : reader.U32();
            if (width != properties.Width || height != properties.Height || reader.Remaining != 0)
            {
                throw new InvalidDataException("Grid header disagrees with image spatial extents.");
            }

            if (!item.References.TryGetValue(HeifBox.Code("dimg"), out IReadOnlyList<uint>? inputs) || inputs.Count != rows * columns)
            {
                throw new InvalidDataException("Grid input count disagrees with its rows and columns.");
            }

            RgbaImage first = decode(inputs[0]);
            int tileWidth = first.Width;
            int tileHeight = first.Height;
            ImageColorInformation color = first.ColorInformation;
            if ((long)tileWidth * columns < width || (long)tileWidth * (columns - 1) >= width ||
                (long)tileHeight * rows < height || (long)tileHeight * (rows - 1) >= height)
            {
                throw new InvalidDataException("Grid tiles do not cover the output rectangle.");
            }

            byte[] pixels = new byte[checked(properties.Width * properties.Height * 4)];
            CopyTile(0, first);
            int workerCount = parallel ? Math.Min(Environment.ProcessorCount, inputs.Count - 1) : 1;
            if (workerCount <= 1)
            {
                for (int index = 1; index < inputs.Count; index++)
                {
                    CopyTile(index, decode(inputs[index]));
                }
            }
            else
            {
                Task[] workers = new Task[workerCount];
                for (int worker = 0; worker < workers.Length; worker++)
                {
                    int firstIndex = worker + 1;
                    workers[worker] = Task.Run(() =>
                    {
                        for (int index = firstIndex; index < inputs.Count; index += workerCount)
                        {
                            CopyTile(index, decode(inputs[index]));
                        }
                    });
                }

                Task.WhenAll(workers).GetAwaiter().GetResult();
            }

            return new RgbaImage(properties.Width, properties.Height, pixels, color);

            void CopyTile(int index, RgbaImage tile)
            {
                if (tile.Width != tileWidth || tile.Height != tileHeight ||
                    tile.ColorInformation.ColorPrimaries != color.ColorPrimaries ||
                    tile.ColorInformation.TransferCharacteristics != color.TransferCharacteristics ||
                    !tile.ColorInformation.IccProfile.Span.SequenceEqual(color.IccProfile.Span))
                {
                    throw new InvalidDataException("Grid tiles have inconsistent dimensions or colour information.");
                }

                int left = index % columns * tileWidth;
                int top = index / columns * tileHeight;
                int copyWidth = Math.Min(tileWidth, properties.Width - left);
                int copyHeight = Math.Min(tileHeight, properties.Height - top);
                for (int y = 0; y < copyHeight; y++)
                {
                    tile.Pixels.Span.Slice(y * tile.Stride, copyWidth * 4).CopyTo(
                        pixels.AsSpan(((top + y) * properties.Width + left) * 4, copyWidth * 4));
                }
            }
        }
    }
}
