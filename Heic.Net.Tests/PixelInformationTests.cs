// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Heic.Net.Heif;
using Xunit;

namespace Heic.Net.Tests
{
    public class PixelInformationTests
    {
        [Theory]
        [InlineData(1, 8)]
        [InlineData(3, 10)]
        public void DescriptivePixelInformationDoesNotOverrideTheCodedPicture(int channels, byte depth)
        {
            byte[] encoded = ReadImage();
            RgbaImage expected = HeicDocument.Load(encoded).DecodePrimary();
            RgbaImage actual = Decode(encoded, channels, depth, false);
            Assert.Equal(expected.Width, actual.Width);
            Assert.Equal(expected.Height, actual.Height);
            Assert.True(expected.Pixels.Span.SequenceEqual(actual.Pixels.Span));
        }

        [Theory]
        [InlineData(1, 8)]
        [InlineData(3, 10)]
        public void InconsistentEssentialPixelInformationIsRejected(int channels, byte depth)
        {
            InvalidDataException error = Assert.Throws<InvalidDataException>(() => Decode(ReadImage(), channels, depth, true));
            Assert.Contains("Pixel information", error.Message);
        }

        private static byte[] ReadImage() => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "corpus", "basic-8bit.heic"));

        private static RgbaImage Decode(byte[] encoded, int channels, byte depth, bool essential)
        {
            HeifContainer source = new HeifContainerReader(HeicDecodeOptions.Default).Read(encoded);
            HeifItem image = source.Items[source.PrimaryItemId];
            byte[] payload = new byte[5 + channels];
            payload[4] = (byte)channels;
            payload.AsSpan(5).Fill(depth);
            HeifBox[] properties = source.Properties.ToArray();
            PropertyAssociation[] associations = image.Associations.ToArray();
            for (int index = 0; index < associations.Length; index++)
            {
                int propertyIndex = associations[index].Index;
                if (properties[propertyIndex].Type == HeifBox.Code("pixi"))
                {
                    properties[propertyIndex] = new HeifBox(HeifBox.Code("pixi"), 0, payload);
                    associations[index] = new PropertyAssociation(propertyIndex, essential);
                }
            }
            ReadOnlyMemory<byte> data = source.ReadData(image.Id);
            HeifItem replacement = new HeifItem(image.Id, image.Type, image.Hidden, 1,
                0, new[] { new ItemExtent(0, (ulong)data.Length) }, associations, image.References);
            Dictionary<uint, HeifItem> items = new Dictionary<uint, HeifItem>(source.Items) { [image.Id] = replacement };
            HeifContainer container = new HeifContainer(encoded, data, source.PrimaryItemId, items, properties, encoded.Length);
            Dictionary<uint, ImageProperties> metadata = new Dictionary<uint, ImageProperties>();
            foreach (HeifItem item in items.Values)
            {
                metadata.Add(item.Id, ImageProperties.Read(container, item, HeicDecodeOptions.Default));
            }
            return new ImageItemDecoder(container, metadata, HeicDecodeOptions.Default).Decode(image.Id);
        }
    }
}
