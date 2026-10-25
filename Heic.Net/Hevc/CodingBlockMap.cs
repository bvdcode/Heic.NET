// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using System;

namespace Heic.Net.Hevc
{
    /// <summary>
    /// Stores slice-local coding metadata in four-luma-sample cells without picture padding.
    /// </summary>
    internal class CodingBlockMap
    {
        private const sbyte UnavailableQp = sbyte.MinValue;
        private readonly byte[] modes;
        private readonly byte[] depths;
        private readonly sbyte[] quantizers;
        private readonly byte[] boundaries;
        private readonly bool[] bypass;

        public CodingBlockMap(int width, int height)
        {
            Width = width / 4;
            Height = height / 4;
            int count = checked(Width * Height);
            modes = new byte[count];
            depths = new byte[count];
            quantizers = new sbyte[count];
            boundaries = new byte[count];
            bypass = new bool[count];
            modes.AsSpan().Fill(byte.MaxValue);
            depths.AsSpan().Fill(byte.MaxValue);
            quantizers.AsSpan().Fill(UnavailableQp);
        }

        public int Width { get; }
        public int Height { get; }
        public int Mode(int x, int y) => Inside(x, y) && modes[Index(x, y)] != byte.MaxValue ? modes[Index(x, y)] : 1;
        public int Depth(int x, int y) => Inside(x, y) && depths[Index(x, y)] != byte.MaxValue ? depths[Index(x, y)] : -1;
        public int Qp(int x, int y)
        {
            if (!Inside(x, y))
            {
                return int.MinValue;
            }

            int qp = quantizers[Index(x, y)];
            return qp == UnavailableQp ? int.MinValue : qp;
        }
        public bool Bypass(int x, int y) => bypass[Index(x, y)];
        public bool Boundary(int x, int y, bool vertical) => (boundaries[Index(x, y)] & (vertical ? 1 : 2)) != 0;

        public void SetCodingUnit(int x, int y, int size, int depth, bool transquantBypass)
        {
            for (int row = y / 4; row < (y + size) / 4; row++)
            {
                depths.AsSpan(row * Width + x / 4, size / 4).Fill((byte)depth);
                bypass.AsSpan(row * Width + x / 4, size / 4).Fill(transquantBypass);
            }

            MarkTransform(x, y, size);
        }

        public void SetMode(int x, int y, int size, int mode)
        {
            for (int row = y / 4; row < (y + size) / 4; row++)
            {
                modes.AsSpan(row * Width + x / 4, size / 4).Fill((byte)mode);
            }
        }

        public void SetQp(int x, int y, int width, int height, int qp)
        {
            if (qp <= UnavailableQp || qp > sbyte.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(qp));
            }

            for (int row = y / 4; row < (y + height) / 4; row++)
            {
                quantizers.AsSpan(row * Width + x / 4, width / 4).Fill((sbyte)qp);
            }
        }

        public void MarkTransform(int x, int y, int size)
        {
            for (int i = 0; i < size; i += 4)
            {
                boundaries[Index(x, y + i)] |= 1;
                boundaries[Index(x + i, y)] |= 2;
            }
        }

        private bool Inside(int x, int y) => x >= 0 && y >= 0 && x < Width * 4 && y < Height * 4;
        private int Index(int x, int y) => y / 4 * Width + x / 4;
    }
}
