// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using System;
using System.Collections.Generic;
using System.IO;

namespace Heic.Net.Fixtures
{
    internal static class ElementaryStream
    {
        public static List<byte[]> Read(byte[] encoded)
        {
            List<byte[]> result = new List<byte[]>();
            int start = -1;
            for (int i = 0; (i + 2) < encoded.Length; i++)
            {
                int prefix = PrefixLength(encoded, i);
                if (prefix == 0)
                {
                    continue;
                }
                if ((start >= 0) && ((i - start) >= 2))
                {
                    result.Add(encoded.AsSpan(start, i - start).ToArray());
                }
                start = i + prefix;
                i = start - 1;
            }
            if ((start >= 0) && ((encoded.Length - start) >= 2))
            {
                result.Add(encoded.AsSpan(start).ToArray());
            }
            if (result.Count == 0)
            {
                throw new InvalidDataException("The fixture stream contains no Annex B NAL units.");
            }
            return result;
        }

        public static byte[] Unescape(byte[] unit)
        {
            List<byte> result = new List<byte>();
            for (int i = 0; i < unit.Length; i++)
            {
                result.Add(unit[i]);
                if ((i >= 1) && (unit[i] == 0) && (unit[i - 1] == 0) && ((i + 1) < unit.Length) && (unit[i + 1] == 3))
                {
                    i++;
                }
            }
            return result.ToArray();
        }

        private static int PrefixLength(byte[] encoded, int index)
        {
            if ((encoded[index] != 0) || (encoded[index + 1] != 0))
            {
                return 0;
            }
            if (encoded[index + 2] == 1)
            {
                return 3;
            }
            if (((index + 3) < encoded.Length) && (encoded[index + 2] == 0) && (encoded[index + 3] == 1))
            {
                return 4;
            }
            return 0;
        }
    }
}
