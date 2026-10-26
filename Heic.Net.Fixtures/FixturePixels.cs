// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using System;

namespace Heic.Net.Fixtures
{
    internal class FixturePixels(int width, int height, byte[] data)
    {
        public int Width { get; } = width;
        public int Height { get; } = height;
        public byte[] Data { get; } = data;

        public FixturePixels Rotate(int turns)
        {
            if ((turns < 1) || (turns > 3))
            {
                throw new ArgumentOutOfRangeException(nameof(turns), turns, "A rotation requires one to three quarter turns.");
            }
            bool swap = turns != 2;
            int targetWidth = swap ? Height : Width;
            int targetHeight = swap ? Width : Height;
            FixturePixels target = new FixturePixels(targetWidth, targetHeight, new byte[Data.Length]);
            for (int y = 0; y < targetHeight; y++)
            {
                for (int x = 0; x < targetWidth; x++)
                {
                    (int sourceX, int sourceY) = turns switch
                    {
                        1 => (Width - y - 1, x),
                        2 => (Width - x - 1, Height - y - 1),
                        3 => (y, Height - x - 1),
                        _ => throw new InvalidOperationException("Invalid rotation.")
                    };
                    CopyPixel(sourceX, sourceY, target, x, y);
                }
            }
            return target;
        }

        public FixturePixels Mirror(int axis)
        {
            if ((axis != 0) && (axis != 1))
            {
                throw new ArgumentOutOfRangeException(nameof(axis), axis, "The mirror axis must be zero or one.");
            }
            FixturePixels target = new FixturePixels(Width, Height, new byte[Data.Length]);
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    CopyPixel(axis == 1 ? Width - x - 1 : x, axis == 0 ? Height - y - 1 : y, target, x, y);
                }
            }
            return target;
        }

        public FixturePixels Crop(int left, int top, int width, int height)
        {
            byte[] target = new byte[checked(width * height * 4)];
            for (int y = 0; y < height; y++)
            {
                Data.AsSpan((((top + y) * Width) + left) * 4, width * 4).CopyTo(target.AsSpan(y * width * 4));
            }
            return new FixturePixels(width, height, target);
        }

        public FixturePixels Tile(int width, int height)
        {
            FixturePixels target = new FixturePixels(width, height, new byte[checked(width * height * 4)]);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    CopyPixel(x % Width, y % Height, target, x, y);
                }
            }
            return target;
        }

        public FixturePixels WithAlpha(FixturePixels alpha)
        {
            if ((alpha.Width != Width) || (alpha.Height != Height))
            {
                throw new ArgumentException("Alpha and colour dimensions must match.", nameof(alpha));
            }
            byte[] target = (byte[])Data.Clone();
            for (int i = 0; i < target.Length; i += 4)
            {
                target[i + 3] = alpha.Data[i];
            }
            return new FixturePixels(Width, Height, target);
        }

        private void CopyPixel(int x, int y, FixturePixels target, int targetX, int targetY)
        {
            Data.AsSpan(((y * Width) + x) * 4, 4).CopyTo(target.Data.AsSpan(((targetY * target.Width) + targetX) * 4));
        }
    }
}
