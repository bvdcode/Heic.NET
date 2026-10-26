// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using System;

namespace Heic.Net.Tests
{
    internal static class ResidualGroupWriter
    {
        public static byte[] Write(ReadOnlySpan<int> magnitudes, int negativeSigns, bool hidden, int qp)
        {
            CabacIntervalWriter writer = new CabacIntervalWriter(qp);
            int[] diagonal = { 0, 4, 1, 8, 5, 2, 12, 9, 6, 3, 13, 10, 7, 14, 11, 15 };
            int[] map = { 0, 1, 4, 5, 2, 3, 4, 5, 6, 6, 8, 8, 7, 7, 8, 8 };
            byte[] prefixInitial = { 110, 110, 124 };
            byte[] significantInitial = { 111, 111, 125, 110, 110, 94, 124, 108, 124 };
            for (int coordinate = 0; coordinate < 2; coordinate++)
            {
                for (int i = 0; i < 3; i++)
                {
                    writer.Decision(true, (coordinate * 10) + i, prefixInitial[i]);
                }
            }
            for (int n = 14; n >= 0; n--)
            {
                int context = map[diagonal[n]];
                writer.Decision(n >= (16 - magnitudes.Length), 100 + context, significantInitial[context]);
            }
            int firstGreater = WriteGreaterFlags(writer, magnitudes);
            int signCount = magnitudes.Length - (hidden ? 1 : 0);
            for (int i = 0; i < signCount; i++)
            {
                writer.Bypass((negativeSigns & (1 << i)) != 0);
            }
            int rice = 0;
            int previousAbsolute = 0;
            for (int i = 0; i < magnitudes.Length; i++)
            {
                int threshold = i < 8 ? (i == firstGreater ? 3 : 2) : 1;
                if (magnitudes[i] >= threshold)
                {
                    if (previousAbsolute > (3 << rice))
                    {
                        rice = Math.Min(4, rice + 1);
                    }
                    writer.RemainingLevel(magnitudes[i] - threshold, rice);
                    previousAbsolute = magnitudes[i];
                }
            }
            writer.Terminate();
            return writer.ToArray();
        }

        private static int WriteGreaterFlags(CabacIntervalWriter writer, ReadOnlySpan<int> magnitudes)
        {
            byte[] greaterInitial = { 140, 92, 137, 138 };
            int greaterContext = 1;
            int firstGreater = -1;
            for (int i = 0; i < Math.Min(magnitudes.Length, 8); i++)
            {
                bool greater = magnitudes[i] > 1;
                int context = Math.Min(greaterContext, 3);
                writer.Decision(greater, 200 + context, greaterInitial[context]);
                if (greater)
                {
                    if (firstGreater < 0)
                    {
                        firstGreater = i;
                    }
                    greaterContext = 0;
                }
                else if (greaterContext > 0)
                {
                    greaterContext++;
                }
            }
            if (firstGreater >= 0)
            {
                writer.Decision(magnitudes[firstGreater] > 2, 300, 138);
            }
            return firstGreater;
        }
    }
}
