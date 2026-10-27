// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov (bvdcode) <https://belov.us>

using System;
using System.IO;
using Heic.Net.Heif;
using Heic.Net.Hevc;
using Xunit;

namespace Heic.Net.Tests
{
    public class AlphaChannelInformationTests
    {
        [Theory]
        [InlineData(8, 0, 255, false, false, false, 128, 128)]
        [InlineData(10, 0, 1023, false, false, false, 512, 128)]
        [InlineData(8, 255, 0, false, false, false, 128, 127)]
        [InlineData(8, 16, 240, false, true, true, 0, 0)]
        [InlineData(8, 16, 240, false, true, true, 255, 255)]
        [InlineData(8, 16, 240, false, true, true, 128, 128)]
        [InlineData(8, 0, 255, false, true, false, 127, 0)]
        [InlineData(8, 0, 255, false, true, false, 128, 255)]
        [InlineData(8, 255, 0, false, true, false, 127, 255)]
        [InlineData(8, 0, 256, true, false, false, 255, 255)]
        [InlineData(8, 0, 256, true, false, false, 0, 0)]
        [InlineData(8, 0, 256, true, true, false, 128, 255)]
        public void AlphaSamplesUseTheSignalledRangeAndOperations(int depth, int transparent, int opaque,
            bool increment, bool clip, bool clipToRange, int sample, byte expected)
        {
            AlphaChannelInformation? information = AlphaChannelInformation.Read(Payload(depth, transparent, opaque, increment, clip, clipToRange));
            Assert.NotNull(information);
            Assert.Equal(depth, information.BitDepth);
            Assert.Equal(expected, information.Convert(sample));
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        public void AuxiliarySeiUsesTheConfigurationNalLengthSize(int lengthSize)
        {
            byte[] payload = Payload(8, 0, 255, false, false, false);
            byte[] nal = HeifTestFile.Join(new byte[] { 0x4e, 1, 165, (byte)payload.Length }, payload, new byte[] { 128 });
            byte[] length = HeifTestFile.U32((uint)nal.Length);
            byte[] stream = HeifTestFile.Join(length.AsSpan(4 - lengthSize).ToArray(), nal);
            byte[] subtype = HeifTestFile.Join(HeifTestFile.U32((uint)stream.Length), stream);
            AlphaChannelInformation? information = HevcAuxiliaryInformation.Read(subtype, lengthSize);
            Assert.NotNull(information);
            Assert.Equal(128, information.Convert(128));
            for (int count = 1; count < subtype.Length; count++)
            {
                int truncatedLength = count;
                Assert.Throws<InvalidDataException>(() => HevcAuxiliaryInformation.Read(subtype.AsMemory(0, truncatedLength), lengthSize));
            }
        }

        [Fact]
        public void LegacyEightBitAlphaEndpointsPreserveTheFullRange()
        {
            byte[] subtype = Convert.FromHexString("0000000C000000084E01A5041001FE40");
            AlphaChannelInformation? information = HevcAuxiliaryInformation.Read(subtype, 4);
            Assert.NotNull(information);
            for (int sample = 0; sample < 256; sample++)
            {
                Assert.Equal((byte)sample, information.Convert(sample));
            }
        }

        [Fact]
        public void CancelledAndEmptyAuxiliaryInformationHaveNoMapping()
        {
            Assert.Null(HevcAuxiliaryInformation.Read(default, 4));
            Assert.Null(HevcAuxiliaryInformation.Read(new byte[4], 4));
            Assert.Null(AlphaChannelInformation.Read(new byte[] { 192 }));
        }

        [Fact]
        public void InvalidPayloadAndNalKindsAreRejected()
        {
            Assert.Throws<InvalidDataException>(() => AlphaChannelInformation.Read(new byte[4]));
            byte[] subtype = Convert.FromHexString("0000000C000000084001A5041001FE40");
            Assert.Throws<InvalidDataException>(() => HevcAuxiliaryInformation.Read(subtype, 4));
            subtype = Convert.FromHexString("0000000C000000084E01A5051001FE40");
            Assert.Throws<InvalidDataException>(() => HevcAuxiliaryInformation.Read(subtype, 4));
        }

        private static byte[] Payload(int depth, int transparent, int opaque, bool increment, bool clip, bool clipToRange)
        {
            TestBitWriter writer = new TestBitWriter();
            writer.Flag(false);
            writer.Write(0, 3);
            writer.Write(depth - 8, 3);
            writer.Write(transparent, depth + 1);
            writer.Write(opaque, depth + 1);
            writer.Flag(increment);
            writer.Flag(clip);
            if (clip)
            {
                writer.Flag(clipToRange);
            }
            writer.TrailingBits();
            return writer.ToArray();
        }
    }
}
