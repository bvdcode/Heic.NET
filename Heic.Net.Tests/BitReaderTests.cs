// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using System.IO;
using Heic.Net.Hevc;
using Xunit;

namespace Heic.Net.Tests
{
    public class BitReaderTests
    {
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
        public void EmulationPreventionBytesAreRemovedOnce()
        {
            NalPayload nal = new NalPayload(new byte[] { 0x40, 1, 0, 0, 3, 1, 0x80 });
            Assert.Equal(new byte[] { 0x40, 1, 0, 0, 1, 0x80 }, nal.Bytes.ToArray());
            Assert.Throws<InvalidDataException>(() => new NalPayload(new byte[] { 0x40, 1, 0, 0, 3, 4 }));
            Assert.Throws<InvalidDataException>(() => NalPayload.ReadLengthPrefixed(new byte[] { 0, 0, 0, 8, 1 }, 4));
        }
    }
}
