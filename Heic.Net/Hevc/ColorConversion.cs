using System;

namespace Heic.Net.Hevc
{
    /// <summary>
    /// Converts nonlinear YCbCr to RGB with Q20 coefficients and a signed 64-bit accumulator.
    /// </summary>
    /// <remarks>
    /// Limited range uses Y 16..235 and C 16..240 scaled by 2^(depth-8). Full range
    /// uses 0..2^depth-1 with chroma centred at 2^(depth-1). Each coefficient is rounded
    /// once to Q20; each final channel rounds by adding 2^19 then shifting and clipping.
    /// This operation does not change the transfer function or colour primaries.
    /// </remarks>
    internal class ColorConversion
    {
        private const int FractionBits = 20;
        private const long Scale = 1L << FractionBits;
        private readonly int yOffset;
        private readonly int chromaOffset;
        private readonly long yFactor;
        private readonly long redCr;
        private readonly long blueCb;
        private readonly long greenCb;
        private readonly long greenCr;

        public ColorConversion(int depth, bool fullRange, int matrix)
        {
            if (depth != 8 && depth != 10)
            {
                throw new ArgumentOutOfRangeException(nameof(depth));
            }
            double kr;
            double kb;
            switch (matrix)
            {
                case 1:
                    kr = 0.2126;
                    kb = 0.0722;
                    break;
                case 5:
                case 6:
                    kr = 0.299;
                    kb = 0.114;
                    break;
                case 9:
                    kr = 0.2627;
                    kb = 0.0593;
                    break;
                default:
                    throw new UnsupportedHeicFeatureException("YCbCr matrix coefficients " + matrix);
            }

            int unit = 1 << (depth - 8);
            int maximum = (1 << depth) - 1;
            int yRange = fullRange ? maximum : 219 * unit;
            int cRange = fullRange ? maximum : 224 * unit;
            yOffset = fullRange ? 0 : 16 * unit;
            chromaOffset = 1 << (depth - 1);
            yFactor = Fixed(255.0 / yRange);
            redCr = Fixed(255.0 / cRange * 2 * (1 - kr));
            blueCb = Fixed(255.0 / cRange * 2 * (1 - kb));
            greenCb = Fixed(-255.0 / cRange * 2 * kb * (1 - kb) / (1 - kr - kb));
            greenCr = Fixed(-255.0 / cRange * 2 * kr * (1 - kr) / (1 - kr - kb));
        }

        public void Convert(int y, int cb, int cr, Span<byte> rgba)
        {
            long luma = yFactor * (y - yOffset);
            int blue = cb - chromaOffset;
            int red = cr - chromaOffset;
            rgba[0] = Channel(luma + redCr * red);
            rgba[1] = Channel(luma + greenCb * blue + greenCr * red);
            rgba[2] = Channel(luma + blueCb * blue);
            rgba[3] = 255;
        }

        public byte Gray(int y) => Channel(yFactor * (y - yOffset));

        private static long Fixed(double value) => (long)Math.Round(value * Scale, MidpointRounding.AwayFromZero);
        private static byte Channel(long value) => (byte)Math.Min(255L, Math.Max(0L, (value + Scale / 2) >> FractionBits));
    }
}
