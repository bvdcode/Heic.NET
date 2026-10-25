using System;

namespace Heic.Net.Hevc
{
    internal static class CoefficientContexts
    {
        private static readonly byte[] SmallMap = { 0, 1, 4, 5, 2, 3, 4, 5, 6, 6, 8, 8, 7, 7, 8, 8 };

        public static int Significant(int x, int y, int log2Size, int scanIndex, int component, ReadOnlySpan<byte> codedGroups)
        {
            int context;
            if (log2Size == 2)
            {
                context = SmallMap[y * 4 + x];
            }
            else if (x + y == 0)
            {
                context = 0;
            }
            else
            {
                int width = 1 << (log2Size - 2);
                int gx = x / 4;
                int gy = y / 4;
                int pattern = 0;
                if (gx + 1 < width)
                {
                    pattern += codedGroups[gy * width + gx + 1];
                }
                if (gy + 1 < width)
                {
                    pattern += 2 * codedGroups[(gy + 1) * width + gx];
                }
                int px = x & 3;
                int py = y & 3;
                switch (pattern)
                {
                    case 0:
                        context = px + py == 0 ? 2 : px + py < 3 ? 1 : 0;
                        break;
                    case 1:
                        context = py == 0 ? 2 : py == 1 ? 1 : 0;
                        break;
                    case 2:
                        context = px == 0 ? 2 : px == 1 ? 1 : 0;
                        break;
                    case 3:
                        context = 2;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(codedGroups));
                }

                if (component == 0)
                {
                    if (gx + gy > 0)
                    {
                        context += 3;
                    }
                    context += log2Size == 3 ? (scanIndex == 0 ? 9 : 15) : 21;
                }
                else
                {
                    context += log2Size == 3 ? 9 : 12;
                }
            }

            return component == 0 ? context : 27 + context;
        }

        public static int Group(int gx, int gy, int width, int component, ReadOnlySpan<byte> codedGroups)
        {
            int neighbours = 0;
            if (gx + 1 < width)
            {
                neighbours += codedGroups[gy * width + gx + 1];
            }
            if (gy + 1 < width)
            {
                neighbours += codedGroups[(gy + 1) * width + gx];
            }
            return (component == 0 ? 0 : 2) + Math.Min(neighbours, 1);
        }
    }
}
