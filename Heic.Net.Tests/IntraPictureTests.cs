// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using Heic.Net.Hevc;
using Xunit;

namespace Heic.Net.Tests
{
    public class IntraPictureTests
    {
        [Theory]
        [InlineData("pattern-8bit.hevc", "20E2086F7C99B58ED66DCA87E9890E8A97A063B2137EA7528B48B09D47C3A67E")]
        [InlineData("pattern-10bit.hevc", "575B3365D65A121FEC8C20972239A0CA8734B239D84F4544D7BF9CBD47F360E8")]
        [InlineData("pattern-mono.hevc", "6783EC165DE90288B02FB0D231A99E677C0E0B725F12B38A8D8EF68E14F5F588")]
        [InlineData("deblock-8bit.hevc", "8631CBB87015332AE5523547DFF33DF421B46718BDABF11188117B92AE5A7981")]
        [InlineData("deblock-10bit.hevc", "715994A6D11DA7B5A78B1203FD992837492B232719AF9810607781A8531CFFCE")]
        [InlineData("sao-8bit.hevc", "FBFEE2D98685369EE0F167741E2023289B2687E14E2F74B77C9282F1D0A31560")]
        [InlineData("sao-10bit.hevc", "5E7EF210B66D73630EBEA8E419495DC7BCB73D1C210671CD31A4E33B6C86D4C1")]
        [InlineData("wpp-8bit.hevc", "5EBACC5688E590C1F36DCF166CE7C1EC3449B7F633BB2F39814C9549A6C36256")]
        [InlineData("wpp-10bit.hevc", "E3E54B71B1EFA1F0B5AC235D5A0415BC9C88AB6385C7BBB654F7697E7442AF90")]
        [InlineData("wpp-mono.hevc", "3CDA530E7B6F60A0831EC43C2FF747B3AE92605FCA5BF6EDD029EC0471B036B9")]
        [InlineData("ctu64-8bit.hevc", "8D862F8599CD4764436C69412ACC19616BD0711700752851529E56489C567DB7")]
        [InlineData("wpp-ctu64-8bit.hevc", "9CBB32E59D49BE3084230ED9F1D0726A05A8DC4FD4CF19C480BAF073E56DFD3E")]
        [InlineData("wpp-quantization-8bit.hevc", "30BF60DF772CD775A499A4D24FB151D58AFC2C6561EFB83B47570D3BF9014B44")]
        [InlineData("wpp-quantization-10bit.hevc", "2A3279C698798E5E9BD13FC3AD16542CF30B350FBE2983CF7E4FA08EAC30F645")]
        [InlineData("wpp-chroma444-8bit.hevc", "BEAE032F216681E1C9CB02781469A87D9451D581F8E3D99C2267B98017C70E4E")]
        [InlineData("wpp-chroma444-10bit.hevc", "A09D742EABF77A8A58E08C778DA179755C64AE6972FC9139BADEEE8506EB9673")]
        [InlineData("scaling-chroma444-8bit.hevc", "C0D037339123BCFD3AABC26E5B0775606A0A1E548C57A1AC026E1EB210583115")]
        [InlineData("wpp-chroma422-8bit.hevc", "88270A4CE534FEAFEF6A9E58A5F49ADA84A0BDFB4AC15BF147CECEC98585AE4A")]
        [InlineData("wpp-chroma422-10bit.hevc", "5DCEE90EBD21E858C1C5EB2E7EED03D8F41087DCE0C953A5D9CA3BCD71C4A73F")]
        [InlineData("lossless-chroma422-8bit.hevc", "800C040790C936DDC6C6112B8992CD419BD8A88F8BCD1C1534697573772328BD")]
        [InlineData("scaling-chroma422-10bit.hevc", "971855F82CCA40AF17A12EDDE955F62E18CA118033C3408AFC873E254B7D801F")]
        public void PatternPictureMatchesIndependentNativePlaneHash(string file, string expectedHash)
        {
            byte[] encoded = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "corpus", file));
            Dictionary<int, SequenceParameters> sequences = new Dictionary<int, SequenceParameters>();
            Dictionary<int, PictureParameters> pictures = new Dictionary<int, PictureParameters>();
            DecodedPicture? decoded = null;
            foreach (NalPayload nal in ReadAnnexB(encoded))
            {
                switch (nal.Type)
                {
                    case 33:
                        SequenceParameters sps = SequenceParameters.Read(nal, HeicDecodeOptions.Default);
                        sequences.Add(sps.Id, sps);
                        break;
                    case 34:
                        PictureParameters pps = PictureParameters.Read(nal, sequences);
                        Assert.Equal(file.StartsWith("wpp-", StringComparison.Ordinal), pps.EntropyCodingSync);
                        pictures.Add(pps.Id, pps);
                        break;
                    case 19:
                    case 20:
                        decoded = new IntraPictureDecoder(IntraSliceHeader.Read(nal, pictures)).Decode();
                        break;
                }
            }

            Assert.NotNull(decoded);
            using (decoded)
            {
                List<byte> samples = new List<byte>();
                AddPlane(samples, decoded.Luma, decoded.Sequence.BitDepth);
                if (decoded.Cb is not null)
                {
                    AddPlane(samples, decoded.Cb, decoded.Sequence.BitDepth);
                }
                if (decoded.Cr is not null)
                {
                    AddPlane(samples, decoded.Cr, decoded.Sequence.BitDepth);
                }
                string hash = Convert.ToHexString(SHA256.HashData(samples.ToArray()));
                Assert.Equal(expectedHash, hash);
            }
        }

        [Theory]
        [InlineData("wpp-chroma422-8bit.hevc")]
        [InlineData("wpp-chroma422-10bit.hevc")]
        [InlineData("lossless-chroma422-8bit.hevc")]
        [InlineData("scaling-chroma422-10bit.hevc")]
        public void HorizontalSubsamplingRetainsOddVisibleHeight(string file)
        {
            byte[] encoded = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "corpus", file));
            NalPayload nal = Assert.Single(ReadAnnexB(encoded), value => value.Type == 33);
            SequenceParameters sequence = SequenceParameters.Read(nal, HeicDecodeOptions.Default);
            Assert.Equal(2, sequence.ChromaFormat);
            Assert.Equal(208, sequence.Width);
            Assert.Equal(152, sequence.Height);
            Assert.Equal(202, sequence.VisibleWidth);
            Assert.Equal(145, sequence.VisibleHeight);
            using DecodedPicture picture = new DecodedPicture(sequence);
            Assert.NotNull(picture.Cb);
            Assert.NotNull(picture.Cr);
            Assert.Equal(104, picture.Cb.Width);
            Assert.Equal(152, picture.Cb.Height);
            Assert.Equal(104, picture.Cr.Width);
            Assert.Equal(152, picture.Cr.Height);
        }

        private static void AddPlane(List<byte> samples, SamplePlane plane, int depth)
        {
            foreach (ushort sample in plane.Samples.Span)
            {
                samples.Add((byte)(sample & 255));
                if (depth == 10)
                {
                    samples.Add((byte)(sample >> 8));
                }
            }
        }

        private static IEnumerable<NalPayload> ReadAnnexB(byte[] data)
        {
            int start = 4;
            for (int i = 4; i <= data.Length; i++)
            {
                if (i == data.Length || i + 4 <= data.Length && data[i] == 0 && data[i + 1] == 0 && data[i + 2] == 0 && data[i + 3] == 1)
                {
                    yield return new NalPayload(data.AsMemory(start, i - start));
                    start = i + 4;
                    i += 3;
                }
            }
        }
    }
}
