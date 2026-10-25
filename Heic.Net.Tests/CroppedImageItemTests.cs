// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using System;
using System.Collections.Generic;
using System.IO;
using Heic.Net.Heif;
using Xunit;

namespace Heic.Net.Tests
{
    public class CroppedImageItemTests
    {
        [Theory]
        [InlineData("basic-8bit.heic", false)]
        [InlineData("basic-10bit.heic", false)]
        [InlineData("monochrome.heic", false)]
        [InlineData("basic-8bit.heic", true)]
        [InlineData("basic-10bit.heic", true)]
        public void ItemTransformOrderMatchesIndependentSequentialTransforms(string file, bool rotateFirst)
        {
            byte[] encoded = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "corpus", file));
            RgbaImage original = HeicDocument.Load(encoded).DecodePrimary();
            using PixelBufferScope buffers = new PixelBufferScope(false);
            List<HeifBox> transforms = new List<HeifBox>();
            RgbaImage expected = original;
            if (rotateFirst)
            {
                Append(new HeifBox(HeifBox.Code("irot"), 0, new byte[] { 1 }));
            }
            Append(HeifTestFile.CleanAperture(expected.Width, expected.Height, 1, 3, expected.Width - 3, expected.Height - 5));
            Append(new HeifBox(HeifBox.Code("imir"), 0, new byte[] { 1 }));
            if (!rotateFirst)
            {
                Append(new HeifBox(HeifBox.Code("irot"), 0, new byte[] { 3 }));
            }

            ImageItemDecoder decoder = CreateDecoder(encoded, transforms, out uint primaryId);
            RgbaImage actual = decoder.Decode(primaryId);
            Assert.Equal(expected.Width, actual.Width);
            Assert.Equal(expected.Height, actual.Height);
            Assert.Equal(expected.Pixels.ToArray(), actual.Pixels.ToArray());
            byte[] preserved = actual.Pixels.ToArray();
            RgbaImage repeated = decoder.Decode(primaryId);
            Assert.Equal(preserved, actual.Pixels.ToArray());
            Assert.Equal(preserved, repeated.Pixels.ToArray());

            void Append(HeifBox property)
            {
                transforms.Add(property);
                expected = ImageTransforms.Apply(expected, property, buffers);
            }
        }

        private static ImageItemDecoder CreateDecoder(byte[] encoded, IReadOnlyList<HeifBox> transforms, out uint primaryId)
        {
            HeifContainer source = new HeifContainerReader(HeicDecodeOptions.Default).Read(encoded);
            primaryId = source.PrimaryItemId;
            HeifItem image = source.Items[primaryId];
            List<HeifBox> properties = new List<HeifBox>(source.Properties);
            List<PropertyAssociation> associations = new List<PropertyAssociation>(image.Associations);
            foreach (HeifBox property in transforms)
            {
                associations.Add(new PropertyAssociation(properties.Count, true));
                properties.Add(property);
            }
            ReadOnlyMemory<byte> data = source.ReadData(image.Id);
            HeifItem replacement = new HeifItem(image.Id, image.Type, image.Hidden, 1, 0,
                new[] { new ItemExtent(0, (ulong)data.Length) }, associations, image.References);
            Dictionary<uint, HeifItem> items = new Dictionary<uint, HeifItem>(source.Items) { [image.Id] = replacement };
            HeifContainer container = new HeifContainer(encoded, data, primaryId, items, properties.ToArray(), encoded.Length);
            Dictionary<uint, ImageProperties> metadata = new Dictionary<uint, ImageProperties>();
            foreach (HeifItem item in items.Values)
            {
                metadata.Add(item.Id, ImageProperties.Read(container, item, HeicDecodeOptions.Default));
            }
            return new ImageItemDecoder(container, metadata, HeicDecodeOptions.Default);
        }
    }
}
