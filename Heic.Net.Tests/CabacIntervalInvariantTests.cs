// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using System;
using System.IO;
using Heic.Net.Hevc;
using Xunit;

namespace Heic.Net.Tests
{
    public class CabacIntervalInvariantTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void EveryInitialOffsetPreservesTheIntervalAcrossMixedBins(bool symbol)
        {
            Random random = new Random(71653);
            byte[] payload = new byte[32];
            for (byte state = 0; state < 64; state++)
            {
                for (int initialOffset = 0; initialOffset < 510; initialOffset++)
                {
                    random.NextBytes(payload);
                    payload[0] = (byte)(initialOffset >> 1);
                    payload[1] = (byte)((payload[1] & 127) | ((initialOffset & 1) << 7));
                    BitReader reader = new BitReader(payload);
                    CabacDecoder decoder = new CabacDecoder(reader);
                    CabacContext context = new CabacContext(state, symbol);
                    bool terminated = false;
                    for (int step = 0; (step < 64) && (reader.BitsRemaining >= 8); step++)
                    {
                        switch (step % 4)
                        {
                            case 0:
                            case 1:
                                decoder.ReadBin(ref context);
                                break;
                            case 2:
                                decoder.ReadBypassBits(7);
                                break;
                            case 3:
                                if (decoder.ReadTermination())
                                {
                                    Assert.Throws<InvalidOperationException>(() => decoder.ReadBin(ref context));
                                    Assert.Throws<InvalidOperationException>(() => decoder.ReadBypass());
                                    Assert.Throws<InvalidOperationException>(() => decoder.ReadTermination());
                                    terminated = true;
                                }
                                break;
                        }
                        if (terminated)
                        {
                            break;
                        }
                        Assert.InRange(decoder.Range, 256, 510);
                        Assert.InRange(decoder.Offset, 0, decoder.Range - 1);
                        Assert.InRange(context.State, 0, 63);
                    }
                }
            }
        }

        [Fact]
        public void InvalidInitializationAndExhaustedRenormalizationAreRejected()
        {
            Assert.Throws<InvalidDataException>(() => new CabacDecoder(new BitReader(Array.Empty<byte>())));
            Assert.Throws<InvalidDataException>(() => new CabacDecoder(new BitReader(new byte[1])));
            foreach (byte tail in new byte[] { 0, 128 })
            {
                Assert.Throws<InvalidDataException>(() => new CabacDecoder(new BitReader(new byte[] { 255, tail })));
            }

            BitReader unaligned = new BitReader(new byte[4]);
            unaligned.ReadBit();
            Assert.Throws<InvalidDataException>(() => new CabacDecoder(unaligned));
            BitReader exact = new BitReader(new byte[] { 254, 255 });
            CabacDecoder decoder = new CabacDecoder(exact);
            CabacContext context = new CabacContext(63, false);
            Assert.True(decoder.ReadBin(ref context));
            Assert.Equal(0, exact.BitsRemaining);
            Assert.Throws<InvalidDataException>(() => decoder.ReadBin(ref context));
        }
    }
}
