using System;
using System.Linq;

namespace Heic.Net.Tests
{
    internal static class IntraOracle
    {
        private static readonly int[] SignedAngles =
        {
            0, 0, 32, 26, 21, 17, 13, 9, 5, 2, 0, -2, -5, -9, -13, -17, -21, -26,
            -32, -26, -21, -17, -13, -9, -5, -2, 0, 2, 5, 9, 13, 17, 21, 26, 32
        };

        public static int[] Predict(int[] top, int[] left, int size, int mode, int depth, bool luma, bool strong)
        {
            int[] horizontal = (int[])top.Clone();
            int[] vertical = (int[])left.Clone();
            if (luma)
            {
                Filter(horizontal, vertical, size, mode, depth, strong);
            }
            int dc = (int)Math.Floor((horizontal.Skip(1).Take(size).Sum() + vertical.Skip(1).Take(size).Sum()) / (2.0 * size) + 0.5);
            int[] result = new int[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    double value;
                    switch (mode)
                    {
                        case 0:
                            double alongRow = ((size - x - 1) * vertical[y + 1] + (x + 1) * horizontal[size + 1]) / (double)size;
                            double alongColumn = ((size - y - 1) * horizontal[x + 1] + (y + 1) * vertical[size + 1]) / (double)size;
                            value = (alongRow + alongColumn) / 2;
                            break;
                        case 1:
                            value = dc;
                            if (luma && size < 32)
                            {
                                if (x == 0 && y == 0)
                                {
                                    value = (vertical[1] + 2 * dc + horizontal[1]) / 4.0;
                                }
                                else if (y == 0)
                                {
                                    value = (horizontal[x + 1] + 3 * dc) / 4.0;
                                }
                                else if (x == 0)
                                {
                                    value = (vertical[y + 1] + 3 * dc) / 4.0;
                                }
                            }

                            break;
                        default:
                            bool fromTop = mode >= 18;
                            int[] main = fromTop ? horizontal : vertical;
                            int[] side = fromTop ? vertical : horizontal;
                            int along = fromTop ? x : y;
                            int across = fromTop ? y : x;
                            double position = along + 1 + (across + 1) * SignedAngles[mode] / 32.0;
                            int lower = (int)Math.Floor(position);
                            double fraction = position - lower;
                            value = At(main, side, lower, SignedAngles[mode]);
                            if (fraction != 0)
                            {
                                value = (1 - fraction) * value + fraction * At(main, side, lower + 1, SignedAngles[mode]);
                            }

                            if (SignedAngles[mode] == 0 && luma && size < 32 && along == 0)
                            {
                                value = Math.Clamp(main[1] + Math.Floor((side[across + 1] - side[0]) / 2.0), 0, (1 << depth) - 1);
                            }

                            break;
                    }

                    result[y * size + x] = (int)Math.Floor(value + 0.5);
                }
            }

            return result;
        }

        public static void Filter(int[] top, int[] left, int size, int mode, int depth, bool strong)
        {
            int threshold = size switch
            {
                4 => int.MaxValue,
                8 => 7,
                16 => 1,
                32 => 0,
                _ => throw new ArgumentOutOfRangeException(nameof(size))
            };
            if (mode == 1 || Math.Min(Math.Abs(mode - 10), Math.Abs(mode - 26)) <= threshold)
            {
                return;
            }
            int length = 2 * size;
            if (strong && size == 32 && Math.Abs(top[0] + top[length] - 2 * top[size]) < Math.Pow(2, depth - 5) &&
                Math.Abs(left[0] + left[length] - 2 * left[size]) < Math.Pow(2, depth - 5))
            {
                int corner = top[0];
                for (int i = 1; i < length; i++)
                {
                    top[i] = (int)Math.Floor(corner + i * (top[length] - corner) / 64.0 + 0.5);
                    left[i] = (int)Math.Floor(corner + i * (left[length] - corner) / 64.0 + 0.5);
                }
            }
            else
            {
                int[] border = left.Reverse().Concat(top.Skip(1)).ToArray();
                for (int i = 1; i < border.Length - 1; i++)
                {
                    int value = (int)Math.Floor((border[i - 1] + 2 * border[i] + border[i + 1]) / 4.0 + 0.5);
                    if (i <= length)
                    {
                        left[length - i] = value;
                    }
                    if (i >= length)
                    {
                        top[i - length] = value;
                    }
                }
            }
        }

        private static int At(int[] main, int[] side, int index, int angle)
        {
            if (index >= 0)
            {
                return main[index];
            }
            double inverse = Math.Round(8192.0 / Math.Abs(angle), MidpointRounding.AwayFromZero);
            return side[(int)Math.Floor(-index * inverse / 256.0 + 0.5)];
        }
    }
}
