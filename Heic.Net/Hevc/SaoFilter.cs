// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using System;
using System.Buffers;

namespace Heic.Net.Hevc
{
    /// <summary>
    /// Reads CTB offsets and applies them after deblocking using original neighbouring rows.
    /// </summary>
    /// <remarks>
    /// Parameters and the two-row ring buffer belong to this worker. Row storage is rented
    /// only during filtering and returned even when filtering fails. In-place writes never
    /// become neighbour inputs. CTB coordinates are in luma units; component coordinates
    /// follow the coded horizontal and vertical subsampling. Picture edges have no edge-offset neighbours.
    /// </remarks>
    internal class SaoFilter
    {
        private readonly IntraSliceHeader slice;
        private readonly IntraCabacReader reader;
        private readonly SaoParameters[] parameters;
        private readonly int columns;
        private readonly int components;
        private readonly bool enabled;

        public SaoFilter(IntraSliceHeader slice, IntraCabacReader reader)
        {
            this.slice = slice;
            this.reader = reader;
            int ctb = 1 << slice.Picture.Sequence.Log2Ctb;
            columns = (slice.Picture.Sequence.Width + ctb - 1) / ctb;
            int rows = (slice.Picture.Sequence.Height + ctb - 1) / ctb;
            components = slice.Picture.Sequence.ChromaFormat == 0 ? 1 : 3;
            enabled = slice.SaoLuma || slice.SaoChroma;
            parameters = enabled ? new SaoParameters[checked(columns * rows * components)] : Array.Empty<SaoParameters>();
        }

        public void ReadCtb(int x, int y)
        {
            if (!enabled)
            {
                return;
            }
            int current = (y * columns + x) * components;
            int previous = -1;
            if (x > 0 && reader.Read(CabacElement.SaoMerge))
            {
                previous = current - components;
            }
            else if (y > 0 && reader.Read(CabacElement.SaoMerge))
            {
                previous = current - columns * components;
            }
            if (previous >= 0)
            {
                Array.Copy(parameters, previous, parameters, current, components);
                return;
            }

            for (int component = 0; component < components; component++)
            {
                if (component == 0 ? slice.SaoLuma : slice.SaoChroma)
                {
                    parameters[current + component] = SaoParameters.Read(reader, component, slice.Picture.Sequence.BitDepth,
                        component == 2 ? parameters[current + 1] : default);
                }
            }
        }

        public void Apply(DecodedPicture picture, CodingBlockMap blocks)
        {
            if (!enabled)
            {
                return;
            }
            if (slice.SaoLuma)
            {
                ApplyPlane(picture.Luma, blocks, 0);
            }
            if (slice.SaoChroma && picture.Cb is not null)
            {
                ApplyPlane(picture.Cb, blocks, 1);
            }
            if (slice.SaoChroma && picture.Cr is not null)
            {
                ApplyPlane(picture.Cr, blocks, 2);
            }
        }

        private void ApplyPlane(SamplePlane plane, CodingBlockMap blocks, int component)
        {
            int depth = slice.Picture.Sequence.BitDepth;
            int scaleX = component == 0 ? 1 : slice.Picture.Sequence.SubWidthC;
            int scaleY = component == 0 ? 1 : slice.Picture.Sequence.SubHeightC;
            int ctbWidth = (1 << slice.Picture.Sequence.Log2Ctb) / scaleX;
            int ctbHeight = (1 << slice.Picture.Sequence.Log2Ctb) / scaleY;
            ushort[] buffer = ArrayPool<ushort>.Shared.Rent(checked(plane.Width * 2));
            try
            {
                for (int y = 0; y < plane.Height; y++)
                {
                    Span<ushort> current = buffer.AsSpan((y & 1) * plane.Width, plane.Width);
                    plane.Row(y).CopyTo(current);
                    ReadOnlySpan<ushort> above = y == 0 ? ReadOnlySpan<ushort>.Empty : buffer.AsSpan(((y - 1) & 1) * plane.Width, plane.Width);
                    ReadOnlySpan<ushort> below = y + 1 == plane.Height ? ReadOnlySpan<ushort>.Empty : plane.Row(y + 1);
                    Span<ushort> output = plane.Row(y);
                    int parameterRow = y / ctbHeight * columns * components + component;
                    for (int start = 0, index = parameterRow; start < plane.Width; start += ctbWidth, index += components)
                    {
                        SaoParameters parameter = parameters[index];
                        if (parameter.Type == 0)
                        {
                            continue;
                        }

                        int end = Math.Min(start + ctbWidth, plane.Width);
                        if (parameter.Type == 1)
                        {
                            for (int x = start; x < end; x++)
                            {
                                if (!blocks.Bypass(x * scaleX, y * scaleY))
                                {
                                    output[x] = (ushort)SaoKernel.Band(current[x], depth, parameter);
                                }
                            }
                        }
                        else
                        {
                            Direction(parameter.Edge, out int dx, out int dy);
                            if (y < Math.Abs(dy) || y >= plane.Height - Math.Abs(dy))
                            {
                                continue;
                            }

                            ReadOnlySpan<ushort> first = NeighbourRow(above, current, below, dy);
                            ReadOnlySpan<ushort> second = NeighbourRow(above, current, below, -dy);
                            int firstX = Math.Max(start, Math.Abs(dx));
                            int lastX = Math.Min(end, plane.Width - Math.Abs(dx));
                            for (int x = firstX; x < lastX; x++)
                            {
                                if (!blocks.Bypass(x * scaleX, y * scaleY))
                                {
                                    output[x] = (ushort)SaoKernel.Edge(current[x], first[x + dx], second[x - dx], depth, parameter);
                                }
                            }
                        }
                    }
                }
            }
            finally
            {
                ArrayPool<ushort>.Shared.Return(buffer);
            }
        }

        private static ReadOnlySpan<ushort> NeighbourRow(ReadOnlySpan<ushort> above, ReadOnlySpan<ushort> current, ReadOnlySpan<ushort> below, int dy)
        {
            switch (dy)
            {
                case -1:
                    return above;
                case 0:
                    return current;
                case 1:
                    return below;
                default:
                    throw new ArgumentOutOfRangeException(nameof(dy));
            }
        }

        private static void Direction(int edge, out int dx, out int dy)
        {
            switch (edge)
            {
                case 0:
                    dx = -1;
                    dy = 0;
                    break;
                case 1:
                    dx = 0;
                    dy = -1;
                    break;
                case 2:
                    dx = -1;
                    dy = -1;
                    break;
                case 3:
                    dx = 1;
                    dy = -1;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(edge));
            }
        }
    }
}
