// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using System;

namespace Heic.Net.Hevc
{
    /// <summary>
    /// Describes the visible rows of three planar components after their crop offsets.
    /// </summary>
    internal readonly struct ColorPlaneLayout
    {
        public ColorPlaneLayout(int width, int height, int lumaStride, int chromaStride, int subWidth, int subHeight)
        {
            if (width < 0 || height < 0 || lumaStride < width)
            {
                throw new ArgumentOutOfRangeException(nameof(width));
            }
            if ((subWidth != 1 && subWidth != 2) || (subHeight != 1 && subHeight != 2))
            {
                throw new ArgumentOutOfRangeException(nameof(subWidth));
            }
            int chromaWidth = checked(width + subWidth - 1) / subWidth;
            if (chromaStride < chromaWidth)
            {
                throw new ArgumentOutOfRangeException(nameof(chromaStride));
            }
            Width = width;
            Height = height;
            LumaStride = lumaStride;
            ChromaStride = chromaStride;
            SubWidth = subWidth;
            SubHeight = subHeight;
            ChromaWidth = chromaWidth;
            int chromaHeight = checked(height + subHeight - 1) / subHeight;
            LumaLength = height == 0 ? 0 : checked((height - 1) * lumaStride + width);
            ChromaLength = chromaHeight == 0 ? 0 : checked((chromaHeight - 1) * chromaStride + chromaWidth);
            OutputLength = checked(width * height * 4);
        }

        public int Width { get; }
        public int Height { get; }
        public int LumaStride { get; }
        public int ChromaStride { get; }
        public int SubWidth { get; }
        public int SubHeight { get; }
        public int ChromaWidth { get; }
        public int LumaLength { get; }
        public int ChromaLength { get; }
        public int OutputLength { get; }
    }
}
