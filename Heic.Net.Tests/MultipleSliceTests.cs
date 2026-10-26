// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using System;
using System.Collections.Generic;
using System.IO;
using Heic.Net.Hevc;
using Xunit;

namespace Heic.Net.Tests
{
    public class MultipleSliceTests
    {
        [Theory]
        [InlineData("missing-first")]
        [InlineData("missing-last")]
        [InlineData("duplicate-second")]
        [InlineData("reverse-tail")]
        public void IncompleteOrReorderedSlicesAreRejected(string modification)
        {
            List<byte[]> units = ReadUnits("slices3-cra-8bit.hevc");
            List<byte[]> slices = units.FindAll(unit => (unit[0] >> 1) == 21);
            units.RemoveAll(unit => (unit[0] >> 1) == 21);
            switch (modification)
            {
                case "missing-first":
                    slices.RemoveAt(0);
                    break;
                case "missing-last":
                    slices.RemoveAt(slices.Count - 1);
                    break;
                case "duplicate-second":
                    slices.Insert(2, slices[1]);
                    break;
                case "reverse-tail":
                    (slices[1], slices[2]) = (slices[2], slices[1]);
                    break;
                default:
                    throw new ArgumentException("Unknown slice modification.", nameof(modification));
            }
            units.AddRange(slices);
            Assert.Throws<InvalidDataException>(() => Decode(units));
        }

        [Fact]
        public void SecondPictureIsDistinctFromAdditionalSlices()
        {
            List<byte[]> units = ReadUnits("slices3-cra-8bit.hevc");
            byte[] first = units.Find(unit => (unit[0] >> 1) == 21)!;
            units.Add(first);
            Assert.Throws<UnsupportedHeicFeatureException>(() => Decode(units));
        }

        private static void Decode(List<byte[]> units)
        {
            byte[] configuration = new byte[23];
            configuration[0] = 1;
            configuration[21] = 3;
            List<byte> data = new List<byte>();
            foreach (byte[] unit in units)
            {
                data.AddRange(HeifTestFile.U32((uint)unit.Length));
                data.AddRange(unit);
            }
            using DecodedPicture picture = HevcImageDecoder.Decode(DecoderConfiguration.Read(configuration), data.ToArray(), HeicDecodeOptions.Default);
        }

        private static List<byte[]> ReadUnits(string file)
        {
            byte[] encoded = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "corpus", file));
            List<byte[]> units = new List<byte[]>();
            int start = 4;
            for (int i = 4; i <= encoded.Length; i++)
            {
                if ((i == encoded.Length) || (((i + 4) <= encoded.Length) && (encoded[i] == 0) && (encoded[i + 1] == 0) && (encoded[i + 2] == 0) && (encoded[i + 3] == 1)))
                {
                    units.Add(encoded.AsSpan(start, i - start).ToArray());
                    start = i + 4;
                    i += 3;
                }
            }
            return units;
        }
    }
}
