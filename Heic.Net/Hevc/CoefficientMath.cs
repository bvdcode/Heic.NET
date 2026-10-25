using System;

namespace Heic.Net.Hevc
{
    internal static class CoefficientMath
    {
        private static readonly int[] LevelScale = { 40, 45, 51, 57, 64, 72 };

        /// <summary>
        /// Applies the normal-precision inverse quantization defined in HEVC section 8.6.3.
        /// </summary>
        /// <param name="coefficient">A signed 16-bit transform coefficient.</param>
        /// <param name="scalingFactor">A scaling-list factor between zero and 255.</param>
        /// <param name="qp">The quantization parameter, including the bit-depth offset.</param>
        /// <param name="log2Size">The base-two logarithm of a block size from 4 through 32.</param>
        /// <param name="bitDepth">The supported sample depth: 8 or 10 bits.</param>
        /// <returns>The rounded and clipped signed 16-bit coefficient.</returns>
        /// <exception cref="ArgumentOutOfRangeException">An input is outside its supported range.</exception>
        /// <remarks>
        /// Multiplication uses a 64-bit accumulator. Rounding adds half the divisor before
        /// an arithmetic right shift, including for negative values. Intermediate products
        /// must not wrap modulo 2^32. Clipping occurs after the rounding shift.
        /// </remarks>
        public static int Dequantize(int coefficient, int scalingFactor, int qp, int log2Size, int bitDepth)
        {
            if (coefficient < short.MinValue || coefficient > short.MaxValue || scalingFactor < 0 || scalingFactor > 255 ||
                (bitDepth != 8 && bitDepth != 10) || qp < 0 || qp > 51 + 6 * (bitDepth - 8) || log2Size < 2 || log2Size > 5)
            {
                throw new ArgumentOutOfRangeException(nameof(coefficient));
            }

            int shift = bitDepth + log2Size - 5;
            long product = (long)coefficient * scalingFactor * LevelScale[qp % 6] * (1L << (qp / 6));
            long scaled = (product + (1L << (shift - 1))) >> shift;
            return (int)Math.Max(short.MinValue, Math.Min(short.MaxValue, scaled));
        }

        public static int ClipSample(int value, int bitDepth) => Math.Max(0, Math.Min((1 << bitDepth) - 1, value));
    }
}
