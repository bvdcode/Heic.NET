using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Heic.Net.Tests
{
    public class HeicDocumentTests
    {
        [Fact]
        public async Task DocumentOwnsInputAndConcurrentCallsOwnTheirOutput()
        {
            byte[] encoded = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "corpus", "basic-8bit.heic"));
            HeicDocument document = HeicDocument.Load(encoded);
            Array.Clear(encoded);
            Task<RgbaImage>[] calls = new Task<RgbaImage>[4];
            for (int i = 0; i < calls.Length; i++)
            {
                calls[i] = Task.Run(document.DecodePrimary);
            }

            RgbaImage[] images = await Task.WhenAll(calls);
            for (int i = 1; i < images.Length; i++)
            {
                Assert.True(images[0].Pixels.Span.SequenceEqual(images[i].Pixels.Span));
                Assert.False(images[0].Pixels.Equals(images[i].Pixels));
            }
        }

        [Fact]
        public void GridCyclesAndDerivationDepthAreRejected()
        {
            byte[] grid = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "corpus", "grid.heic"));
            int reference = grid.AsSpan().IndexOf(HeifTestFile.Text("dimg"));
            Assert.True(reference >= 0);
            grid[reference + 8] = 0;
            grid[reference + 9] = 5;
            Assert.Throws<InvalidDataException>(() => HeicDocument.Load(grid).DecodePrimary());
            byte[] identity = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "corpus", "identity.heic"));
            Assert.Throws<InvalidDataException>(() => HeicDocument.Load(identity,
                new HeicDecodeOptions(maxDerivationDepth: 1)).DecodePrimary());
        }

        [Fact]
        public void LargeImagesRequireAnExplicitPixelLimit()
        {
            byte[] encoded = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "corpus", "20260223_110514.heic"));
            Assert.Throws<InvalidDataException>(() => HeicDocument.Load(encoded));
        }

        [Fact]
        public async Task AsyncLoadingHonorsBoundsCancellationAndStreamOwnership()
        {
            byte[] bytes = HeifTestFile.Minimal();
            using MemoryStream stream = new MemoryStream(bytes);
            await Assert.ThrowsAsync<InvalidDataException>(() => HeicDocument.LoadAsync(stream,
                new HeicDecodeOptions(maxInputBytes: bytes.Length - 1)));
            Assert.True(stream.CanRead);
            stream.Position = 0;
            using CancellationTokenSource cancellation = new CancellationTokenSource();
            await cancellation.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => HeicDocument.LoadAsync(stream,
                cancellationToken: cancellation.Token));
        }
    }
}
