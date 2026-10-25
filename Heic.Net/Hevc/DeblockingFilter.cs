// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using System;

namespace Heic.Net.Hevc
{
    internal static class DeblockingFilter
    {
        public static void Apply(DecodedPicture picture, IntraSliceHeader slice, CodingBlockMap blocks)
        {
            for (int direction = 0; direction < 2; direction++)
            {
                bool vertical = direction == 0;
                int acrossEnd = vertical ? picture.Sequence.Width : picture.Sequence.Height;
                int alongEnd = vertical ? picture.Sequence.Height : picture.Sequence.Width;
                for (int across = 8; across < acrossEnd; across += 8)
                {
                    for (int along = 0; along < alongEnd; along += 4)
                    {
                        int x = vertical ? across : along;
                        int y = vertical ? along : across;
                        if (blocks.Boundary(x, y, vertical))
                        {
                            LumaEdge(picture.Luma, slice, blocks, x, y, vertical);
                        }
                    }
                }

                if (picture.Cb is not null && picture.Cr is not null)
                {
                    ChromaEdges(picture.Cb, slice, blocks, vertical, slice.Picture.CbQpOffset);
                    ChromaEdges(picture.Cr, slice, blocks, vertical, slice.Picture.CrQpOffset);
                }
            }
        }

        private static void LumaEdge(SamplePlane plane, IntraSliceHeader slice, CodingBlockMap blocks, int x, int y, bool vertical)
        {
            int px = vertical ? x - 1 : x;
            int py = vertical ? y : y - 1;
            int qp = (blocks.Qp(x, y) + blocks.Qp(px, py) + 1) >> 1;
            int depth = slice.Picture.Sequence.BitDepth;
            int beta = DeblockingThresholds.Beta(qp + slice.BetaOffset, depth);
            int tc = DeblockingThresholds.Tc(qp + 2 + slice.TcOffset, depth);
            if (beta == 0 || tc == 0)
            {
                return;
            }

            Span<int> samples = stackalloc int[32];
            ReadLumaEdge(plane, samples, x, y, vertical);
            DeblockingKernel.Luma(samples, beta, tc, depth, !blocks.Bypass(px, py), !blocks.Bypass(x, y));
            WriteLumaEdge(plane, samples, x, y, vertical);
        }

        private static void ReadLumaEdge(SamplePlane plane, Span<int> samples, int x, int y, bool vertical)
        {
            if (vertical)
            {
                for (int k = 0; k < 4; k++)
                {
                    ReadOnlySpan<ushort> row = plane.Row(y + k).Slice(x - 4, 8);
                    for (int i = 0; i < 8; i++)
                    {
                        samples[k * 8 + i] = row[i];
                    }
                }
            }
            else
            {
                for (int i = 0; i < 8; i++)
                {
                    ReadOnlySpan<ushort> row = plane.Row(y + i - 4).Slice(x, 4);
                    for (int k = 0; k < 4; k++)
                    {
                        samples[k * 8 + i] = row[k];
                    }
                }
            }
        }

        private static void WriteLumaEdge(SamplePlane plane, ReadOnlySpan<int> samples, int x, int y, bool vertical)
        {
            if (vertical)
            {
                for (int k = 0; k < 4; k++)
                {
                    Span<ushort> row = plane.Row(y + k).Slice(x - 3, 6);
                    for (int i = 0; i < 6; i++)
                    {
                        row[i] = (ushort)samples[k * 8 + i + 1];
                    }
                }
            }
            else
            {
                for (int i = 1; i <= 6; i++)
                {
                    Span<ushort> row = plane.Row(y + i - 4).Slice(x, 4);
                    for (int k = 0; k < 4; k++)
                    {
                        row[k] = (ushort)samples[k * 8 + i];
                    }
                }
            }
        }

        private static void ChromaEdges(SamplePlane plane, IntraSliceHeader slice, CodingBlockMap blocks, bool vertical, int qpOffset)
        {
            int acrossEnd = vertical ? plane.Width : plane.Height;
            int alongEnd = vertical ? plane.Height : plane.Width;
            int depth = slice.Picture.Sequence.BitDepth;
            Span<int> samples = stackalloc int[4];
            for (int across = 8; across < acrossEnd; across += 8)
            {
                for (int along = 0; along < alongEnd; along += 4)
                {
                    int x = vertical ? across : along;
                    int y = vertical ? along : across;
                    if (!blocks.Boundary(x * 2, y * 2, vertical))
                    {
                        continue;
                    }
                    int px = vertical ? x * 2 - 1 : x * 2;
                    int py = vertical ? y * 2 : y * 2 - 1;
                    int qp = ((blocks.Qp(x * 2, y * 2) + blocks.Qp(px, py) + 1) >> 1) + qpOffset;
                    int tc = DeblockingThresholds.Tc(QuantizationMath.Chroma(qp) + 2 + slice.TcOffset, depth);
                    if (tc == 0)
                    {
                        continue;
                    }
                    for (int k = 0; k < 4; k++)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            samples[i] = plane.Row(y + (vertical ? k : i - 2))[x + (vertical ? i - 2 : k)];
                        }

                        bool writeP = !blocks.Bypass(px + (vertical ? 0 : k * 2), py + (vertical ? k * 2 : 0));
                        bool writeQ = !blocks.Bypass((x + (vertical ? 0 : k)) * 2, (y + (vertical ? k : 0)) * 2);
                        DeblockingKernel.Chroma(samples, tc, depth, writeP, writeQ);
                        for (int i = 1; i <= 2; i++)
                        {
                            plane.Row(y + (vertical ? k : i - 2))[x + (vertical ? i - 2 : k)] = (ushort)samples[i];
                        }
                    }
                }
            }
        }
    }
}
