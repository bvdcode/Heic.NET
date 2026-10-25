// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using System;
using System.IO;
using Heic.Net.Hevc;
using Xunit;

namespace Heic.Net.Tests
{
    public class BitReaderTests
    {
        [Fact]
        public void IndividualBitsInterleaveWithFieldsPeeksAndSkips()
        {
            byte[] data = new byte[257];
            Random random = new Random(197);
            random.NextBytes(data);
            BitReader reader = new BitReader(data);
            int position = 0;
            while (position < data.Length * 8)
            {
                int count = Math.Min(random.Next(1, 33), data.Length * 8 - position);
                uint expected = 0;
                for (int bit = 0; bit < count; bit++)
                {
                    int at = position + bit;
                    expected = (expected << 1) | (uint)((data[at / 8] >> (7 - at % 8)) & 1);
                }
                Assert.Equal(unchecked((int)expected), reader.Peek(count));
                Assert.Equal((ulong)position, reader.GetBitPosition());
                switch (random.Next(3))
                {
                    case 0:
                        reader.SkipBits(count);
                        break;
                    case 1:
                        Assert.Equal(unchecked((int)expected), reader.Read(count));
                        break;
                    case 2:
                        for (int bit = 0; bit < count; bit++)
                        {
                            Assert.Equal((int)((expected >> (count - bit - 1)) & 1), reader.ReadBit());
                        }
                        break;
                }
                position += count;
                Assert.Equal((ulong)position, reader.GetBitPosition());
                Assert.Equal(data.Length * 8 - position, reader.BitsRemaining);
            }
            Assert.Throws<InvalidDataException>(() => reader.ReadBit());
            Assert.Equal((ulong)(data.Length * 8), reader.GetBitPosition());
            Assert.Throws<InvalidDataException>(() => new BitReader(ReadOnlyMemory<byte>.Empty).ReadBit());
        }

        [Fact]
        public void ReadsAcrossByteBoundariesAndPeeksWithoutConsumption()
        {
            BitReader reader = new BitReader(new byte[] { 0xb6, 0x71 });
            Assert.Equal(5, reader.Read(3));
            Assert.Equal(22, reader.Peek(5));
            Assert.Equal(359, reader.Read(9));
            Assert.Equal(1, reader.Read(4));
            Assert.Throws<InvalidDataException>(() => reader.Read(1));
        }

        [Fact]
        public void ExpGolombIsBoundedAndSigned()
        {
            BitReader reader = new BitReader(new byte[] { 0xa6, 0x40 });
            Assert.Equal(0u, reader.ReadUev());
            Assert.Equal(1, reader.ReadSev());
            Assert.Equal(-1, reader.ReadSev());
            Assert.Throws<InvalidDataException>(() => new BitReader(new byte[8]).ReadUev());
        }

        [Fact]
        public void EntropySubstreamOffsetsIncludeEmulationPreventionBytes()
        {
            NalPayload nal = new NalPayload(new byte[]
            {
                0x28, 1, 0, 0, 3, 1, 0x80,
                0, 0, 3, 2, 0x80,
                0, 0, 3, 0, 0x80,
                1, 2
            });
            ReadOnlyMemory<byte>[] streams = nal.Substreams(6, new[] { 5, 5 });
            Assert.Equal(3, streams.Length);
            Assert.Equal(new byte[] { 0, 0, 2, 0x80 }, streams[0].ToArray());
            Assert.Equal(new byte[] { 0, 0, 0, 0x80 }, streams[1].ToArray());
            Assert.Equal(new byte[] { 1, 2 }, streams[2].ToArray());
            Assert.Throws<InvalidDataException>(() => nal.Substreams(-1, Array.Empty<int>()));
            Assert.Throws<InvalidDataException>(() => nal.Substreams(nal.Bytes.Length, Array.Empty<int>()));
            Assert.Throws<InvalidDataException>(() => nal.Substreams(6, new[] { 0 }));
            Assert.Throws<InvalidDataException>(() => nal.Substreams(6, new[] { 5, 7 }));
            Assert.Throws<InvalidDataException>(() => nal.Substreams(6, new[] { int.MaxValue }));
        }

        [Fact]
        public void EmulationPreventionBytesAreRemovedOnce()
        {
            NalPayload nal = new NalPayload(new byte[] { 0x40, 1, 0, 0, 3, 1, 0x80 });
            Assert.Equal(new byte[] { 0x40, 1, 0, 0, 1, 0x80 }, nal.Bytes.ToArray());
            Assert.Throws<InvalidDataException>(() => new NalPayload(new byte[] { 0x40, 1, 0, 0, 3, 4 }));
            Assert.Throws<InvalidDataException>(() => NalPayload.ReadLengthPrefixed(new byte[] { 0, 0, 0, 8, 1 }, 4));
        }
    }
}
