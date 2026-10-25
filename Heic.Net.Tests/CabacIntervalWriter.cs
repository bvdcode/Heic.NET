// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using System;
using System.Collections.Generic;
using System.Numerics;
using Heic.Net.Hevc;

namespace Heic.Net.Tests
{
    /// <summary>
    /// Constructs a code point by subdividing exact arbitrary-precision intervals.
    /// </summary>
    /// <remarks>
    /// This oracle emits no bits during normalization. Its complete interval remains in
    /// a fixed 4096-bit coordinate space; only the unit relating that space to HEVC's
    /// probability table changes. Normative range/transition constants are shared.
    /// </remarks>
    internal class CabacIntervalWriter(int qp)
    {
        private const int CodeBits = 4096;
        private readonly Dictionary<int, (int State, bool Mps)> contexts = new Dictionary<int, (int, bool)>();
        private BigInteger low;
        private BigInteger unit = BigInteger.One << (CodeBits - 9);
        private BigInteger width = 510 * (BigInteger.One << (CodeBits - 9));
        private int range = 510;

        public void Decision(bool value, int slot, byte initial)
        {
            if (!contexts.TryGetValue(slot, out (int State, bool Mps) context))
            {
                int pre = Math.Clamp((int)Math.Floor(((initial / 16) * 5 - 45) * Math.Clamp(qp, 0, 51) / 16.0) + (initial % 16) * 8 - 16, 1, 126);
                context = pre < 64 ? (63 - pre, false) : (pre - 64, true);
            }

            int lps = CabacTables.LpsRange(context.State, range);
            int mpsRange = range - lps;
            if (value == context.Mps)
            {
                range = mpsRange;
                context.State = CabacTables.NextMps(context.State);
            }
            else
            {
                low += unit * mpsRange;
                range = lps;
                if (context.State == 0)
                {
                    context.Mps = !context.Mps;
                }
                context.State = CabacTables.NextLps(context.State);
            }

            width = range * unit;
            while (range < 256)
            {
                range *= 2;
                unit /= 2;
            }

            contexts[slot] = context;
        }

        public void Bypass(bool value)
        {
            width /= 2;
            unit /= 2;
            if (value)
            {
                low += width;
            }
        }

        public void Bits(int value, int count)
        {
            for (int i = count - 1; i >= 0; i--)
            {
                Bypass((value & (1 << i)) != 0);
            }
        }

        public void ExpGolomb(int value, int order)
        {
            while (value >= 1 << order)
            {
                Bypass(true);
                value -= 1 << order++;
            }

            Bypass(false);
            Bits(value, order);
        }

        public void RemainingLevel(int value, int rice)
        {
            int prefix = Math.Min(value >> rice, 4);
            for (int i = 0; i < prefix; i++)
            {
                Bypass(true);
            }
            if (prefix < 4)
            {
                Bypass(false);
                Bits(value & ((1 << rice) - 1), rice);
            }
            else
            {
                ExpGolomb(value - (4 << rice), rice + 1);
            }
        }

        public void Terminate()
        {
            low += (range - 2) * unit;
            width = 2 * unit;
        }

        public byte[] ToArray()
        {
            byte[] point = (low + width / 2).ToByteArray(isUnsigned: true, isBigEndian: true);
            byte[] result = new byte[CodeBits / 8];
            point.CopyTo(result.AsSpan(result.Length - point.Length));
            return result;
        }
    }
}
