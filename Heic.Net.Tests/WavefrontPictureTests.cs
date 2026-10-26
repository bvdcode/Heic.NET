// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using System;
using System.IO;
using System.Threading.Tasks;
using Heic.Net.Hevc;
using Xunit;

namespace Heic.Net.Tests
{
    public class WavefrontPictureTests
    {
        [Theory]
        [InlineData("wpp-8bit.hevc")]
        [InlineData("wpp-10bit.hevc")]
        [InlineData("wpp-mono.hevc")]
        [InlineData("wpp-quantization-8bit.hevc")]
        [InlineData("wpp-quantization-10bit.hevc")]
        [InlineData("wpp-chroma444-10bit.hevc")]
        [InlineData("wpp-chroma422-10bit.hevc")]
        public void RowSchedulingPreservesPlanesAcrossWorkerCounts(string file)
        {
            string expected;
            using (DecodedPicture sequential = new IntraPictureDecoder(IntraPictureFixture.ReadSlice(file)).Decode())
            {
                expected = IntraPictureFixture.PlaneHash(sequential);
            }
            for (int repetition = 0; repetition < 3; repetition++)
            {
                foreach (int workers in new[] { 2, 3, 4 })
                {
                    using DecodedPicture actual = new IntraPictureDecoder(IntraPictureFixture.ReadSlice(file)).Decode(workers);
                    Assert.Equal(expected, IntraPictureFixture.PlaneHash(actual));
                }
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(-1)]
        public async Task InvalidRowStopsAllWorkersWithoutWaitingForMissingProgress(int row)
        {
            IntraSliceHeader slice = IntraPictureFixture.ReadSlice("wpp-quantization-10bit.hevc");
            int index = row < 0 ? slice.RowSubstreams.Count - 1 : row;
            ArraySegment<byte> bytes = slice.RowSubstreams[index];
            bytes.Array![bytes.Offset] = 255;
            bytes.Array[bytes.Offset + 1] |= 128;

            Task<DecodedPicture> decoding = Task.Run(() => new IntraPictureDecoder(slice).Decode(4));
            await Assert.ThrowsAsync<InvalidDataException>(async () =>
            {
                using DecodedPicture result = await decoding.WaitAsync(TimeSpan.FromSeconds(15));
            });
        }
    }
}
