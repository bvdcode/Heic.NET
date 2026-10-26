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
            Assert.Throws<InvalidDataException>(() => new BitReader(ArraySegment<byte>.Empty).ReadBit());
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
        public void SegmentReadsHonorOffsetAndLengthWithoutConsumingPadding()
        {
            byte[] data = { 0xff, 0xb6, 0x71, 0xff };
            BitReader reader = new BitReader(new ArraySegment<byte>(data, 1, 2));
            Assert.Equal(16, reader.BitsRemaining);
            Assert.Equal(1, reader.ReadBit());
            Assert.Equal(13, reader.Peek(5));
            Assert.Equal(1ul, reader.GetBitPosition());
            Assert.Equal(13, reader.Read(5));
            reader.SkipBits(2);
            Assert.Equal(0x71, reader.Read(8));
            Assert.Equal(0, reader.BitsRemaining);
            Assert.Throws<InvalidDataException>(() => reader.ReadBit());
            Assert.Throws<InvalidDataException>(() => reader.Peek(1));
            Assert.Throws<InvalidDataException>(() => reader.SkipBits(1));
            Assert.Equal(16ul, reader.GetBitPosition());
            Assert.Equal(0, reader.Read(0));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void EmptySegmentsRemainReadableOnlyForZeroBits(bool hasBackingArray)
        {
            ArraySegment<byte> data = default;
            if (hasBackingArray)
            {
                data = new ArraySegment<byte>(new byte[] { 0xff, 0xff }, 1, 0);
            }
            BitReader reader = new BitReader(data);
            Assert.Equal(0, reader.BitsRemaining);
            Assert.Equal(0, reader.Read(0));
            Assert.Equal(0, reader.Peek(0));
            reader.SkipBits(0);
            Assert.Throws<InvalidDataException>(() => reader.ReadBit());
            Assert.Throws<InvalidDataException>(() => reader.Read(1));
            Assert.Throws<InvalidDataException>(() => reader.SkipBits(1));
            Assert.Equal(0ul, reader.GetBitPosition());
        }

        [Fact]
        public void RejectedSegmentReadsPreservePosition()
        {
            BitReader reader = new BitReader(new ArraySegment<byte>(new byte[] { 0xff, 0xb6, 0xff }, 1, 1));
            Assert.Equal(5, reader.Read(3));
            Assert.Throws<InvalidDataException>(() => reader.Read(6));
            Assert.Throws<InvalidDataException>(() => reader.Peek(6));
            Assert.Throws<InvalidDataException>(() => reader.SkipBits(6));
            Assert.Throws<InvalidDataException>(() => reader.SkipBits(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => reader.Read(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => reader.Read(33));
            Assert.Equal(3ul, reader.GetBitPosition());
            Assert.Equal(22, reader.Read(5));
        }

        [Fact]
        public void NalPayloadOwnsDecodedBytesFromManagedMemory()
        {
            byte[] encoded = { 0x40, 1, 0, 0, 3, 1, 0x80 };
            using ManagedByteMemory memory = new ManagedByteMemory(encoded);
            NalPayload nal = new NalPayload(memory.Memory);
            Array.Clear(encoded);
            BitReader reader = new BitReader(nal.Bytes.Slice(2));
            Assert.Equal(1, reader.Read(24));
            Assert.Equal(0x80, reader.Read(8));
            Assert.Equal(0, reader.BitsRemaining);
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
            ArraySegment<byte>[] streams = nal.Substreams(6, new[] { 5, 5 });
            Assert.Equal(3, streams.Length);
            Assert.Equal(new byte[] { 0, 0, 2, 0x80 }, streams[0].ToArray());
            Assert.Equal(new byte[] { 0, 0, 0, 0x80 }, streams[1].ToArray());
            Assert.Equal(new byte[] { 1, 2 }, streams[2].ToArray());
            Assert.Throws<InvalidDataException>(() => nal.Substreams(-1, Array.Empty<int>()));
            Assert.Throws<InvalidDataException>(() => nal.Substreams(nal.Bytes.Count, Array.Empty<int>()));
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
