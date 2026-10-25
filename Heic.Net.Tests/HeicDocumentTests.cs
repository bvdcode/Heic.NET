using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Heic.Net.Tests
{
    public class HeicDocumentTests
    {
        [Fact]
        public void OriginalCameraGridDecodesThroughPublicApi()
        {
            byte[] encoded = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "corpus", "20260201_164721.heic"));
            Assert.Equal("74621A8EA1CB5A6F4ED3C06F31C87DEA455371611764EF181ED290AC48ED77B8", Convert.ToHexString(SHA256.HashData(encoded)));
            HeicDocument document = HeicDocument.Load(encoded);
            Array.Clear(encoded);
            RgbaImage image = document.DecodePrimary();
            Assert.Equal(4000, image.Width);
            Assert.Equal(3000, image.Height);
            Assert.Equal(48000000, image.Pixels.Length);
            Assert.Equal(5, image.ColorInformation.ColorPrimaries);
            Assert.Equal(6, image.ColorInformation.TransferCharacteristics);
            Assert.Equal("CD66C3B43E6A8D1F0319AAE29EC30EF38B85DB54C84855EB1E09D5ED5181F705",
                Convert.ToHexString(SHA256.HashData(image.Pixels.Span)));
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
