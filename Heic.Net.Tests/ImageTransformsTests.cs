using System;
using System.IO;
using Heic.Net.Heif;
using Xunit;

namespace Heic.Net.Tests
{
    public class ImageTransformsTests
    {
        [Theory]
        [InlineData(0, null, 3, 2, new byte[] { 1, 2, 3, 4, 5, 6 })]
        [InlineData(1, null, 2, 3, new byte[] { 3, 6, 2, 5, 1, 4 })]
        [InlineData(2, null, 3, 2, new byte[] { 6, 5, 4, 3, 2, 1 })]
        [InlineData(3, null, 2, 3, new byte[] { 4, 1, 5, 2, 6, 3 })]
        [InlineData(0, 0, 3, 2, new byte[] { 4, 5, 6, 1, 2, 3 })]
        [InlineData(0, 1, 3, 2, new byte[] { 3, 2, 1, 6, 5, 4 })]
        [InlineData(1, 0, 2, 3, new byte[] { 1, 4, 2, 5, 3, 6 })]
        public void OrientationMatchesLabelledPixelPositions(int turns, int? axis, int width, int height, byte[] labels)
        {
            RgbaImage source = Source();
            RgbaImage result = ImageTransforms.Orient(source, turns, axis);
            Assert.Equal(width, result.Width);
            Assert.Equal(height, result.Height);
            for (int i = 0; i < labels.Length; i++)
            {
                Assert.Equal(labels[i], result.Pixels.Span[i * 4]);
            }

            Assert.Equal(1, source.Pixels.Span[0]);
        }

        [Fact]
        public void CleanApertureUsesExactRationalCoordinates()
        {
            byte[] payload = HeifTestFile.Join(HeifTestFile.U32(1), HeifTestFile.U32(1),
                HeifTestFile.U32(2), HeifTestFile.U32(1), HeifTestFile.U32(0), HeifTestFile.U32(1),
                HeifTestFile.U32(0), HeifTestFile.U32(1));
            RgbaImage result = ImageTransforms.Apply(Source(), new HeifBox(HeifBox.Code("clap"), 0, payload));
            Assert.Equal(1, result.Width);
            Assert.Equal(new byte[] { 2, 0, 0, 255, 5, 0, 0, 255 }, result.Pixels.ToArray());
            Array.Clear(payload, 4, 4);
            Assert.Throws<InvalidDataException>(() => ImageTransforms.Apply(Source(), new HeifBox(HeifBox.Code("clap"), 0, payload)));
        }

        [Fact]
        public void OriginalCameraMetadataHasBoundedGridExtents()
        {
            byte[] file = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "corpus", "20260201_164721.heic"));
            HeifContainer container = new HeifContainerReader(HeicDecodeOptions.Default).Read(file);
            HeifItem primary = container.Items[container.PrimaryItemId];
            ImageProperties properties = ImageProperties.Read(container, primary, HeicDecodeOptions.Default);
            Assert.Equal(HeifBox.Code("grid"), primary.Type);
            Assert.Equal(12000000, properties.Width * properties.Height);
            Assert.Equal(48, primary.References[HeifBox.Code("dimg")].Count);
        }

        private static RgbaImage Source() => new RgbaImage(3, 2, new byte[]
        {
            1, 0, 0, 255, 2, 0, 0, 255, 3, 0, 0, 255,
            4, 0, 0, 255, 5, 0, 0, 255, 6, 0, 0, 255
        }, new ImageColorInformation(1, 1));
    }
}
