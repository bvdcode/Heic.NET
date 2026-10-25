// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using System;

namespace Heic.Net.Hevc
{
    internal static class IntegerTransform
    {
        private static readonly int[] CosineMagnitudes =
        {
            90, 90, 90, 90, 89, 88, 87, 85, 83, 82, 80, 78, 75, 73, 70, 67,
            64, 61, 57, 54, 50, 46, 43, 38, 36, 31, 25, 22, 18, 13, 9, 4, 0
        };

        private static readonly int[] Sine4 =
        {
            29, 55, 74, 84,
            74, 74, 0, -74,
            84, -29, -74, 55,
            55, -84, 74, -29
        };

        /// <summary>
        /// Applies the HEVC inverse integer transform to one square coefficient block.
        /// </summary>
        /// <param name="coefficients">Row-major signed 16-bit coefficients; index = y * size + x.</param>
        /// <param name="residual">Worker-owned destination of at least size * size integers.</param>
        /// <param name="size">The transform width and height: 4, 8, 16 or 32.</param>
        /// <param name="bitDepth">The reconstructed sample depth: 8 or 10.</param>
        /// <param name="intraLuma">Whether a four-sample luma block requires the inverse sine transform.</param>
        /// <param name="workspace">Scratch storage owned exclusively by the current worker.</param>
        /// <remarks>
        /// The first, vertical pass adds 64, shifts arithmetically by seven bits and clips
        /// to signed 16-bit. The horizontal pass adds half the divisor and shifts by
        /// 20 - bitDepth. No input or output includes picture padding. Accumulators are
        /// 64-bit; unchecked integer overflow is never part of the transform contract.
        /// Input and destination may overlap because the first pass reads all coefficients.
        /// </remarks>
        public static void Inverse(ReadOnlySpan<int> coefficients, Span<int> residual, int size,
            int bitDepth, bool intraLuma, TransformWorkspace workspace)
        {
            if ((size != 4 && size != 8 && size != 16 && size != 32) || (bitDepth != 8 && bitDepth != 10))
            {
                throw new ArgumentOutOfRangeException(nameof(size));
            }

            int count = size * size;
            if (coefficients.Length < count || residual.Length < count)
            {
                throw new ArgumentException("Transform buffers are shorter than the block.");
            }

            for (int i = 0; i < count; i++)
            {
                if (coefficients[i] < short.MinValue || coefficients[i] > short.MaxValue)
                {
                    throw new ArgumentOutOfRangeException(nameof(coefficients));
                }
            }

            Span<int> intermediate = workspace.Intermediate.Slice(0, count);
            bool sine = size == 4 && intraLuma;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    long sum = 0;
                    for (int frequency = 0; frequency < size; frequency++)
                    {
                        int value = coefficients[frequency * size + x];
                        if (value != 0)
                        {
                            sum += (long)Basis(frequency, y, size, sine) * value;
                        }
                    }

                    intermediate[y * size + x] = (int)Math.Max(short.MinValue, Math.Min(short.MaxValue, (sum + 64) >> 7));
                }
            }

            int shift = 20 - bitDepth;
            long offset = 1L << (shift - 1);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    long sum = 0;
                    for (int frequency = 0; frequency < size; frequency++)
                    {
                        int value = intermediate[y * size + frequency];
                        if (value != 0)
                        {
                            sum += (long)Basis(frequency, x, size, sine) * value;
                        }
                    }

                    residual[y * size + x] = checked((int)((sum + offset) >> shift));
                }
            }
        }

        private static int Basis(int frequency, int position, int size, bool sine)
        {
            if (sine)
            {
                return Sine4[frequency * 4 + position];
            }
            if (frequency == 0)
            {
                return 64;
            }
            int angle = ((2 * position + 1) * frequency * (32 / size)) & 127;
            if (angle > 64)
            {
                angle = 128 - angle;
            }
            return angle > 32 ? -CosineMagnitudes[64 - angle] : CosineMagnitudes[angle];
        }
    }
}
