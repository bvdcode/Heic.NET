// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using System;

namespace Heic.Net.Hevc
{
    /// <summary>
    /// Owns a reconstructed component plane and its four-sample availability map.
    /// </summary>
    /// <remarks>
    /// Samples[y * Stride + x] has no border padding; Width/Height are coded component
    /// dimensions. The plane belongs to one decoded picture and is never rented or shared
    /// with another decoding operation. Availability is published after each complete TU.
    /// </remarks>
    internal class SamplePlane
    {
        private readonly ushort[] samples;
        private readonly bool[] available;

        public SamplePlane(int width, int height)
        {
            if (width <= 0 || height <= 0 || width % 4 != 0 || height % 4 != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width));
            }

            Width = width;
            Height = height;
            samples = new ushort[checked(width * height)];
            available = new bool[width / 4 * (height / 4)];
        }

        public int Width { get; }
        public int Height { get; }
        public int Stride => Width;
        public ReadOnlyMemory<ushort> Samples => samples;
        public Span<ushort> Row(int y) => samples.AsSpan(y * Stride, Width);

        public int Neighbour(int x, int y)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height || !available[y / 4 * (Width / 4) + x / 4])
            {
                return -1;
            }
            return samples[y * Stride + x];
        }

        public void Store(int x, int y, int size, ReadOnlySpan<int> prediction, ReadOnlySpan<int> residual, int depth)
        {
            for (int row = 0; row < size; row++)
            {
                Span<ushort> destination = Row(y + row).Slice(x, size);
                for (int column = 0; column < size; column++)
                {
                    int index = row * size + column;
                    destination[column] = (ushort)CoefficientMath.ClipSample(prediction[index] + residual[index], depth);
                }
            }

            for (int row = y / 4; row < (y + size) / 4; row++)
            {
                available.AsSpan(row * (Width / 4) + x / 4, size / 4).Fill(true);
            }
        }
    }
}
