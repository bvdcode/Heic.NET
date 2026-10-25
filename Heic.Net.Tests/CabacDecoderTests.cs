using System;
using System.IO;
using Heic.Net.Hevc;
using Xunit;

namespace Heic.Net.Tests
{
    public class CabacDecoderTests
    {
        [Theory]
        [InlineData(154, 26, 0, true)]
        [InlineData(139, 22, 1, true)]
        [InlineData(139, -1, 8, true)]
        [InlineData(139, 100, 7, false)]
        public void InitializesContextFromSlopeAndIntercept(byte value, int qp, byte state, bool mps)
        {
            CabacContext context = CabacContext.Initialize(value, qp);
            Assert.Equal(state, context.State);
            Assert.Equal(mps, context.MostProbableSymbol);
        }

        [Fact]
        public void MostProbableDecisionAdvancesStateWithoutRenormalizing()
        {
            CabacDecoder decoder = new CabacDecoder(new BitReader(new byte[4]));
            CabacContext context = new CabacContext(0, false);
            Assert.False(decoder.ReadBin(ref context));
            Assert.Equal(1, context.State);
            Assert.Equal(270, decoder.Range);
            Assert.Equal(0, decoder.Offset);
        }

        [Fact]
        public void LeastProbableDecisionAtStateZeroFlipsTheSymbol()
        {
            CabacDecoder decoder = new CabacDecoder(new BitReader(new byte[] { 0xfe, 0x80, 0, 0 }));
            CabacContext context = new CabacContext(0, false);
            Assert.True(decoder.ReadBin(ref context));
            Assert.True(context.MostProbableSymbol);
            Assert.Equal(0, context.State);
            Assert.Equal(480, decoder.Range);
            Assert.Equal(478, decoder.Offset);
        }

        [Fact]
        public void BypassPreservesTheProbabilityRange()
        {
            CabacDecoder decoder = new CabacDecoder(new BitReader(new byte[] { 0x96, 0, 0 }));
            Assert.True(decoder.ReadBypass());
            Assert.Equal(510, decoder.Range);
            Assert.Equal(90, decoder.Offset);
        }

        [Fact]
        public void TerminationPreventsFurtherConsumption()
        {
            CabacDecoder decoder = new CabacDecoder(new BitReader(new byte[] { 0xfe, 0x80 }));
            Assert.True(decoder.ReadTermination());
            Assert.Throws<InvalidOperationException>(() => decoder.ReadBypass());
            Assert.Throws<InvalidDataException>(() => new CabacDecoder(new BitReader(new byte[] { 0xff, 0x80 })));
        }
    }
}
