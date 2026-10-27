// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov (bvdcode) <https://belov.us>

using System;
using System.Buffers;
using System.Collections.Generic;

namespace Heic.Net.Tests
{
    internal class TrackingArrayPool<T>(T poison) : ArrayPool<T>
    {
        private readonly HashSet<T[]> outstanding = new HashSet<T[]>();
        private readonly List<T[]> available = new List<T[]>();
        private readonly List<T[]> buffers = new List<T[]>();
        private int attempts;

        public int FailOnRent { get; set; }
        public int Outstanding => outstanding.Count;
        public int Returns { get; private set; }
        public IReadOnlyList<T[]> Buffers => buffers;

        public override T[] Rent(int minimumLength)
        {
            if (++attempts == FailOnRent)
            {
                throw new InvalidOperationException("Requested rental failure.");
            }
            T[]? buffer = null;
            for (int i = 0; i < available.Count; i++)
            {
                if (available[i].Length >= minimumLength)
                {
                    buffer = available[i];
                    available.RemoveAt(i);
                    break;
                }
            }
            if (buffer is null)
            {
                buffer = new T[minimumLength + 17];
                buffers.Add(buffer);
            }
            Array.Fill(buffer, poison);
            outstanding.Add(buffer);
            return buffer;
        }

        public override void Return(T[] array, bool clearArray = false)
        {
            if (!outstanding.Remove(array))
            {
                throw new InvalidOperationException("Array was returned without an active rental.");
            }
            if (clearArray)
            {
                Array.Clear(array);
            }
            available.Add(array);
            Returns++;
        }
    }
}
