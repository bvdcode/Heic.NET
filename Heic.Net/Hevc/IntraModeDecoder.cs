using System;

namespace Heic.Net.Hevc
{
    internal class IntraModeDecoder(IntraCabacReader reader, CodingBlockMap blocks, int log2Ctb)
    {
        public void ReadLuma(int x, int y, int size, bool split)
        {
            int count = split ? 4 : 1;
            int blockSize = split ? size / 2 : size;
            Span<bool> previous = stackalloc bool[4];
            for (int i = 0; i < count; i++)
            {
                previous[i] = reader.Read(CabacElement.PreviousIntraLuma);
            }
            Span<int> candidates = stackalloc int[3];
            for (int i = 0; i < count; i++)
            {
                int px = x + (i & 1) * blockSize;
                int py = y + (i >> 1) * blockSize;
                int left = blocks.Mode(px - 1, py);
                int top = (py & ((1 << log2Ctb) - 1)) == 0 ? 1 : blocks.Mode(px, py - 1);
                Candidates(left, top, candidates);
                int mode;
                if (previous[i])
                {
                    mode = candidates[reader.Unary(2)];
                }
                else
                {
                    mode = reader.Bits(5);
                    if (candidates[0] > candidates[1])
                    {
                        (candidates[0], candidates[1]) = (candidates[1], candidates[0]);
                    }
                    if (candidates[0] > candidates[2])
                    {
                        (candidates[0], candidates[2]) = (candidates[2], candidates[0]);
                    }
                    if (candidates[1] > candidates[2])
                    {
                        (candidates[1], candidates[2]) = (candidates[2], candidates[1]);
                    }
                    foreach (int candidate in candidates)
                    {
                        if (mode >= candidate)
                        {
                            mode++;
                        }
                    }
                }

                blocks.SetMode(px, py, blockSize, mode);
            }
        }

        public int ReadChroma(int lumaMode)
        {
            if (!reader.Read(CabacElement.ChromaMode))
            {
                return lumaMode;
            }
            int mode;
            switch (reader.Bits(2))
            {
                case 0:
                    mode = 0;
                    break;
                case 1:
                    mode = 26;
                    break;
                case 2:
                    mode = 10;
                    break;
                case 3:
                    mode = 1;
                    break;
                default:
                    throw new InvalidOperationException("Invalid chroma mode index.");
            }

            return mode == lumaMode ? 34 : mode;
        }

        private static void Candidates(int left, int top, Span<int> candidates)
        {
            if (left == top)
            {
                if (left < 2)
                {
                    candidates[0] = 0;
                    candidates[1] = 1;
                    candidates[2] = 26;
                }
                else
                {
                    candidates[0] = left;
                    candidates[1] = 2 + (left + 29) % 32;
                    candidates[2] = 2 + (left - 1) % 32;
                }
            }
            else
            {
                candidates[0] = left;
                candidates[1] = top;
                candidates[2] = left != 0 && top != 0 ? 0 : left != 1 && top != 1 ? 1 : 26;
            }
        }
    }
}
