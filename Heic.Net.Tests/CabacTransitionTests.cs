// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov (bvdcode) <https://belov.us>

using System;
using Heic.Net.Hevc;
using Xunit;

namespace Heic.Net.Tests
{
    public class CabacTransitionTests
    {
        private static readonly byte[] LeastProbableStates =
        {
            0, 0, 1, 2, 2, 4, 4, 5, 6, 7, 8, 9, 9, 11, 11, 12,
            13, 13, 15, 15, 16, 16, 18, 18, 19, 19, 21, 21, 22, 22, 23, 24,
            24, 25, 26, 26, 27, 27, 28, 29, 29, 30, 30, 30, 31, 32, 32, 33,
            33, 33, 34, 34, 35, 35, 35, 36, 36, 36, 37, 37, 37, 38, 38, 63
        };

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void EveryStateMatchesNormativeTransitionAndInterval(bool symbol, bool leastProbable)
        {
            for (byte state = 0; state < 64; state++)
            {
                int initialOffset = leastProbable ? 509 : 0;
                byte[] bits = { (byte)(initialOffset >> 1), (byte)((initialOffset & 1) << 7), 0, 0 };
                BitReader reader = new BitReader(bits);
                CabacDecoder decoder = new CabacDecoder(reader);
                CabacContext context = new CabacContext(state, symbol);
                int lpsRange = CabacTables.LpsRange(state, 510);
                int expectedState = state == 63 ? 63 : Math.Min(62, state + 1);
                if (leastProbable)
                {
                    expectedState = LeastProbableStates[state];
                }
                bool expectedSymbol = symbol != ((state == 0) && leastProbable);
                int expectedRange = leastProbable ? lpsRange : 510 - lpsRange;
                int expectedOffset = leastProbable ? initialOffset - (510 - lpsRange) : initialOffset;
                ulong expectedPosition = 9;
                while (expectedRange < 256)
                {
                    expectedRange *= 2;
                    expectedOffset *= 2;
                    expectedPosition++;
                }

                Assert.Equal(symbol != leastProbable, decoder.ReadBin(ref context));
                Assert.Equal(expectedState, context.State);
                Assert.Equal(expectedSymbol, context.MostProbableSymbol);
                Assert.Equal(expectedRange, decoder.Range);
                Assert.Equal(expectedOffset, decoder.Offset);
                Assert.Equal(expectedPosition, reader.GetBitPosition());
            }
        }

        [Fact]
        public void AllValidStatesAndSymbolsRoundTripAndCopiesRemainIndependent()
        {
            for (byte state = 0; state < 64; state++)
            {
                foreach (bool symbol in new[] { false, true })
                {
                    CabacContext context = new CabacContext(state, symbol);
                    CabacContext copy = context;
                    CabacDecoder decoder = new CabacDecoder(new BitReader(new byte[4]));
                    decoder.ReadBin(ref context);
                    Assert.Equal(state, copy.State);
                    Assert.Equal(symbol, copy.MostProbableSymbol);
                }
            }
            CabacContext zero = default;
            Assert.Equal(0, zero.State);
            Assert.False(zero.MostProbableSymbol);
        }

        [Theory]
        [InlineData(64)]
        [InlineData(127)]
        [InlineData(128)]
        [InlineData(255)]
        public void InvalidInitialStateIsRejected(byte state)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CabacContext(state, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CabacContext(state, true));
        }
    }
}
