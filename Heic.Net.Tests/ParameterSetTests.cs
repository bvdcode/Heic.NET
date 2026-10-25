using System;
using System.Collections.Generic;
using System.IO;
using Heic.Net.Hevc;
using Xunit;

namespace Heic.Net.Tests
{
    public class ParameterSetTests
    {
        private const string Sps8 = "4201010408000003009FA800000300001EA02083165BAB930BC05A808080820000030002000003000210";
        private const string Sps10 = "4201010408000003009DA800000300001EA020831365BAB930BC05A80808082000000300200000030021";
        private const string SpsMono = "4201010408000003009FE800000300001EC0820C596EAE4C2F016E020202080000030008000003000840";

        [Theory]
        [InlineData(Sps8, 8, 1)]
        [InlineData(Sps10, 10, 1)]
        [InlineData(SpsMono, 8, 0)]
        public void ReadsIndependentEncoderParameterSets(string encoded, int depth, int chroma)
        {
            SequenceParameters sps = ReadSequence(encoded);
            Assert.Equal(64, sps.Width);
            Assert.Equal(48, sps.Height);
            Assert.Equal(64, sps.VisibleWidth);
            Assert.Equal(48, sps.VisibleHeight);
            Assert.Equal(depth, sps.BitDepth);
            Assert.Equal(chroma, sps.ChromaFormat);
            Assert.Equal(3, sps.Log2MinCodingBlock);
            Assert.Equal(5, sps.Log2Ctb);
            Assert.Equal(2, sps.Log2MinTransform);
            Assert.Equal(5, sps.Log2MaxTransform);
            Assert.Equal(1, sps.Video.MatrixCoefficients);
            Assert.Equal(1, sps.Video.TransferCharacteristics);
            Assert.Equal(1, sps.Video.ColourPrimaries);
            Assert.Equal(chroma == 0, sps.Video.FullRange);

            Dictionary<int, SequenceParameters> sequences = new Dictionary<int, SequenceParameters> { [0] = sps };
            PictureParameters pps = PictureParameters.Read(new NalPayload(Convert.FromHexString("4401C173C089")), sequences);
            Assert.Same(sps, pps.Sequence);
            Assert.Equal(26, pps.InitialQp);
            Assert.True(pps.SignDataHiding);
            Assert.True(pps.CuQpDelta);
            Assert.Equal(0, pps.QpDeltaDepth);
        }

        [Fact]
        public void PixelLimitAppliesBeforePictureAllocation()
        {
            NalPayload nal = new NalPayload(Convert.FromHexString(Sps8));
            Assert.Throws<InvalidDataException>(() => SequenceParameters.Read(nal, new HeicDecodeOptions(maxPixels: 1024)));
        }

        [Fact]
        public void EveryTruncatedParameterSetIsRejected()
        {
            byte[] encoded = Convert.FromHexString(Sps10);
            for (int length = 0; length < encoded.Length; length++)
            {
                int count = length;
                Assert.Throws<InvalidDataException>(() => SequenceParameters.Read(new NalPayload(encoded.AsMemory(0, count)), HeicDecodeOptions.Default));
            }
        }

        [Fact]
        public void PictureParameterSetsRequireTheirDeclaredSequence()
        {
            NalPayload nal = new NalPayload(Convert.FromHexString("4401C173C089"));
            Assert.Throws<InvalidDataException>(() => PictureParameters.Read(nal, new Dictionary<int, SequenceParameters>()));
        }

        [Theory]
        [InlineData(true, false)]
        [InlineData(false, true)]
        public void UnsupportedEntropyPartitioningIsRejected(bool tiles, bool wavefront)
        {
            TestBitWriter bits = new TestBitWriter();
            bits.Ue(0);
            bits.Ue(0);
            bits.Write(0, 5);
            bits.Flag(true);
            bits.Flag(false);
            bits.Ue(0);
            bits.Ue(0);
            bits.Se(0);
            bits.Write(0, 3);
            bits.Se(0);
            bits.Se(0);
            bits.Write(0, 4);
            bits.Flag(tiles);
            bits.Flag(wavefront);
            NalPayload nal = new NalPayload(HeifTestFile.Join(new byte[] { 0x44, 1 }, bits.ToArray()));
            Assert.Throws<UnsupportedHeicFeatureException>(() => PictureParameters.Read(nal,
                new Dictionary<int, SequenceParameters> { [0] = ReadSequence(Sps8) }));
        }

        [Fact]
        public void IntraSliceStartsCabacAfterValidatedByteAlignment()
        {
            SequenceParameters sps = ReadSequence(Sps8);
            PictureParameters pps = PictureParameters.Read(new NalPayload(Convert.FromHexString("4401C173C089")),
                new Dictionary<int, SequenceParameters> { [0] = sps });
            NalPayload nal = new NalPayload(Convert.FromHexString("2801ADE0254620C5764792D4AE8C8387"));
            IntraSliceHeader slice = IntraSliceHeader.Read(nal, new Dictionary<int, PictureParameters> { [0] = pps });
            Assert.Same(pps, slice.Picture);
            Assert.Equal(25, slice.Qp);
            Assert.True(slice.Reader.ByteAligned());
            Assert.Equal(32ul, slice.Reader.GetBitPosition());
            Assert.False(slice.SaoLuma);
            Assert.False(slice.SaoChroma);
        }

        private static SequenceParameters ReadSequence(string encoded) =>
            SequenceParameters.Read(new NalPayload(Convert.FromHexString(encoded)), HeicDecodeOptions.Default);
    }
}
