using System;
using System.Collections.Generic;
using System.Numerics;

namespace Heic.Net.Tests
{
    internal class TestBitWriter
    {
        private readonly List<byte> bytes = new List<byte>();
        private int bitCount;

        public void Write(int value, int count)
        {
            for (int i = count - 1; i >= 0; i--)
            {
                if ((bitCount & 7) == 0) { bytes.Add(0); }
                bytes[bitCount >> 3] |= (byte)(((uint)value >> i & 1) << (7 - (bitCount & 7)));
                bitCount++;
            }
        }

        public void Flag(bool value) => Write(value ? 1 : 0, 1);

        public void Ue(uint value)
        {
            uint code = checked(value + 1);
            int leading = 31 - BitOperations.LeadingZeroCount(code);
            Write(0, leading);
            Write(unchecked((int)code), leading + 1);
        }

        public void Se(int value) => Ue(checked((uint)(value > 0 ? 2 * value - 1 : -2 * value)));

        public void TrailingBits()
        {
            Flag(true);
            while ((bitCount & 7) != 0) { Flag(false); }
        }

        public byte[] ToArray() => bytes.ToArray();
    }
}
