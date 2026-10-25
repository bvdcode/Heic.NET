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
                        pictures.Add(pps.Id, pps);
                        break;
                    case 19:
                    case 20:
                        decoded = new IntraPictureDecoder(IntraSliceHeader.Read(nal, pictures)).Decode();
                        break;
                }
            }

            Assert.NotNull(decoded);
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
