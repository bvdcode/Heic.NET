// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using System;
using System.Buffers;

namespace Heic.Net.Hevc
{
    /// <summary>
    /// Rents a reconstructed component plane and its four-sample availability map.
    /// </summary>
    /// <remarks>
    /// Samples[y * Stride + x] has no border padding; Width/Height are coded component
    /// dimensions. Rentals are exclusive to one decoded picture, cleared before use and
    /// returned after rendering. Availability is published after each complete TU.
    /// Plane views must not escape the picture lifetime.
    /// </remarks>
    internal class SamplePlane : IDisposable
    {
        private readonly ushort[] samples;
        private readonly bool[] available;
        private bool disposed;

        public SamplePlane(int width, int height)
        {
            if (width <= 0 || height <= 0 || width % 4 != 0 || height % 4 != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width));
            }

            Width = width;
            Height = height;
            int count = checked(width * height);
            samples = ArrayPool<ushort>.Shared.Rent(count);
            bool initialized = false;
            try
            {
                available = ArrayPool<bool>.Shared.Rent(count / 16);
                samples.AsSpan(0, count).Clear();
                available.AsSpan(0, count / 16).Clear();
                initialized = true;
            }
            finally
            {
                if (!initialized)
                {
                    ArrayPool<ushort>.Shared.Return(samples);
                }
            }
        }

        public int Width { get; }
        public int Height { get; }
        public int Stride => Width;
        public ReadOnlyMemory<ushort> Samples
        {
            get
            {
                ThrowIfDisposed();
                return samples.AsMemory(0, Width * Height);
            }
        }

        public Span<ushort> Row(int y)
        {
            ThrowIfDisposed();
            return samples.AsSpan(y * Stride, Width);
        }

        public int Neighbour(int x, int y)
        {
            ThrowIfDisposed();
            if (x < 0 || y < 0 || x >= Width || y >= Height || !available[y / 4 * (Width / 4) + x / 4])
            {
                return -1;
            }
            return samples[y * Stride + x];
        }

        public void Store(int x, int y, int size, ReadOnlySpan<int> prediction, ReadOnlySpan<int> residual, int depth)
        {
            ThrowIfDisposed();
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

        public void Dispose()
        {
            if (!disposed)
            {
                disposed = true;
                ArrayPool<ushort>.Shared.Return(samples);
                ArrayPool<bool>.Shared.Return(available);
            }
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(SamplePlane));
            }
        }
    }
}
