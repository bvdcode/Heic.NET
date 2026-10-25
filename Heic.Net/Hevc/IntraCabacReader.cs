// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using System;
using System.IO;

namespace Heic.Net.Hevc
{
    /// <summary>
    /// Owns the arithmetic engine and adaptive contexts of one independent I slice.
    /// </summary>
    internal class IntraCabacReader
    {
        private static readonly byte[][] InitialValues =
        {
            new byte[] { 139, 141, 157 },
            new byte[] { 154 },
            new byte[] { 184 },
            new byte[] { 184 },
            new byte[] { 63 },
            new byte[] { 153, 138, 138 },
            new byte[] { 111, 141 },
            new byte[] { 94, 138, 182, 154 },
            new byte[] { 154, 154 },
            new byte[] { 139, 139 },
            new byte[] { 110,110,124,125,140,153,125,127,140,109,111,143,127,111,79,108,123,63 },
            new byte[] { 110,110,124,125,140,153,125,127,140,109,111,143,127,111,79,108,123,63 },
            new byte[] { 91,171,134,141 },
            new byte[]
            {
                111,111,125,110,110,94,124,108,124,107,125,141,179,153,125,107,
                125,141,179,153,125,107,125,141,179,153,125,140,139,182,182,152,
                136,152,136,153,136,139,111,136,139,111,155,154
            },
            new byte[]
            {
                140,92,137,138,140,152,138,139,153,74,149,92,139,107,122,152,
                140,179,166,182,140,227,122,197
            },
            new byte[] { 138,153,136,167,152,152 },
            new byte[] { 153 },
            new byte[] { 200 }
        };

        private readonly CabacDecoder decoder;
        private readonly CabacContext[][] contexts;

        public IntraCabacReader(BitReader reader, int sliceQp)
        {
            decoder = new CabacDecoder(reader);
            contexts = new CabacContext[InitialValues.Length][];
            for (int group = 0; group < contexts.Length; group++)
            {
                contexts[group] = new CabacContext[InitialValues[group].Length];
                for (int i = 0; i < contexts[group].Length; i++)
                {
                    contexts[group][i] = CabacContext.Initialize(InitialValues[group][i], sliceQp);
                }
            }
        }

        public bool Read(CabacElement element, int index = 0) => decoder.ReadBin(ref contexts[(int)element][index]);
        public bool Bypass() => decoder.ReadBypass();
        public int Bits(int count) => unchecked((int)decoder.ReadBypassBits(count));
        public bool Termination() => decoder.ReadTermination();

        public int Unary(int maximum)
        {
            int value = 0;
            while (value < maximum && Bypass())
            {
                value++;
            }
            return value;
        }

        public int ExpGolomb(int order)
        {
            int value = 0;
            while (Bypass())
            {
                if (order >= 16)
                {
                    throw new InvalidDataException("CABAC Exp-Golomb value exceeds coefficient precision.");
                }
                value += 1 << order++;
            }

            return value + Bits(order);
        }

        public int RemainingLevel(int rice)
        {
            if (rice < 0 || rice > 4)
            {
                throw new ArgumentOutOfRangeException(nameof(rice));
            }
            int prefix = Unary(4);
            if (prefix == 4)
            {
                return (4 << rice) + ExpGolomb(rice + 1);
            }
            return (prefix << rice) + Bits(rice);
        }

        public int QpDelta()
        {
            int magnitude = 0;
            while (magnitude < 5 && Read(CabacElement.QpDelta, magnitude == 0 ? 0 : 1))
            {
                magnitude++;
            }
            if (magnitude == 5)
            {
                magnitude += ExpGolomb(0);
            }
            if (magnitude > 38)
            {
                throw new InvalidDataException("Luma QP delta exceeds the 8/10-bit range.");
            }
            return magnitude != 0 && Bypass() ? -magnitude : magnitude;
        }
    }
}
