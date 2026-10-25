// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Heic.Net.Heif;
using Xunit;

namespace Heic.Net.Tests
{
    public class HeicDocumentTests
    {
        [Theory]
        [InlineData("basic-8bit.heic")]
        [InlineData("grid.heic")]
        [InlineData("grid-alpha.heic")]
        public async Task DocumentOwnsInputAndConcurrentCallsOwnTheirOutput(string file)
        {
            byte[] encoded = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "corpus", file));
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
        public void ReturnedPixelsSurviveDifferentSubsequentPictures()
        {
            string corpus = Path.Combine(AppContext.BaseDirectory, "corpus");
            HeicDocument first = HeicDocument.Load(File.ReadAllBytes(Path.Combine(corpus, "basic-8bit.heic")));
            RgbaImage image = first.DecodePrimary();
            byte[] pixels = image.Pixels.ToArray();
            foreach (string file in new[] { "basic-10bit.heic", "monochrome.heic", "grid-alpha.heic", "sao.heic" })
            {
                HeicDocument.Load(File.ReadAllBytes(Path.Combine(corpus, file))).DecodePrimary();
                Assert.True(image.Pixels.Span.SequenceEqual(pixels));
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

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public void LaterGridBranchesPreserveCycleErrors(int tile)
        {
            byte[] grid = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "corpus", "grid.heic"));
            int reference = grid.AsSpan().IndexOf(HeifTestFile.Text("dimg"));
            Assert.True(reference >= 0);
            grid[reference + 8 + tile * 2] = 0;
            grid[reference + 9 + tile * 2] = 5;
            Assert.Throws<InvalidDataException>(() => HeicDocument.Load(grid).DecodePrimary());
        }

        [Fact]
        public void LargeImagesLoadByDefaultAndHonorExplicitPixelLimits()
        {
            byte[] encoded = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "corpus", "20260223_110514.heic"));
            Assert.NotEqual(0u, HeicDocument.Load(encoded).PrimaryItemId);
            Assert.Throws<InvalidDataException>(() => HeicDocument.Load(encoded,
                new HeicDecodeOptions(maxPixels: 16 * 1024 * 1024)));
        }

        [Fact]
        public void UnknownPropertiesHonorTheirEssentialFlag()
        {
            byte[] encoded = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "corpus", "rotate-90.heic"));
            HeifContainer container = new HeifContainerReader(HeicDecodeOptions.Default).Read(encoded);
            foreach (HeifBox property in container.Properties)
            {
                if (property.Type == HeifBox.Code("irot"))
                {
                    HeifTestFile.Text("zzzz").CopyTo(encoded.AsSpan(property.Offset - 4, 4));
                }
            }

            Assert.Throws<UnsupportedHeicFeatureException>(() => HeicDocument.Load(encoded));
            int associations = encoded.AsSpan().IndexOf(HeifTestFile.Text("ipma"));
            const int LastAssociationOffset = 19;
            encoded[associations + LastAssociationOffset] &= 127;
            RgbaImage image = HeicDocument.Load(encoded).DecodePrimary();
            Assert.Equal(64, image.Width);
            Assert.Equal(48, image.Height);
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
