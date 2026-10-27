// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov (bvdcode) <https://belov.us>

using System;
using System.Collections.Generic;
using System.IO;
using Heic.Net.Heif;
using Xunit;

namespace Heic.Net.Tests
{
    public class OverlayImageTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void LayerOrderSignedOffsetsAndCanvasClippingArePreserved(bool wide)
        {
            RgbaImage first = Image(3, 2, new byte[]
            {
                1, 0, 0, 255, 2, 0, 0, 255, 3, 0, 0, 255,
                4, 0, 0, 255, 5, 0, 0, 255, 6, 0, 0, 255
            });
            RgbaImage second = Image(2, 2, new byte[]
            {
                7, 0, 0, 255, 8, 0, 0, 255,
                9, 0, 0, 255, 10, 0, 0, 255
            });
            byte[] payload = Payload(3, 2, new ushort[] { 0, 0, 0, 65535 }, new[] { (-1, 0), (1, 1) }, wide);
            RgbaImage result = Decode(payload, 3, 2, first, second);
            Assert.Equal(new byte[]
            {
                2, 0, 0, 255, 3, 0, 0, 255, 0, 0, 0, 255,
                5, 0, 0, 255, 7, 0, 0, 255, 8, 0, 0, 255
            }, result.Pixels.ToArray());
        }

        [Fact]
        public void StraightAlphaCompositionRetainsBothLayerWeights()
        {
            byte[] payload = Payload(1, 1, new ushort[] { 0, 0, 65535, 32896 }, new[] { (0, 0) });
            RgbaImage result = Decode(payload, 1, 1, Image(1, 1, new byte[] { 255, 0, 0, 128 }));
            Assert.Equal(new byte[] { 170, 0, 85, 192 }, result.Pixels.ToArray());
        }

        [Fact]
        public void TransparentPixelsDoNotChangeOpaqueBackground()
        {
            byte[] payload = Payload(1, 1, new ushort[] { 65535, 0, 0, 65535 }, new[] { (0, 0) });
            RgbaImage result = Decode(payload, 1, 1, Image(1, 1, new byte[] { 0, 0, 255, 0 }));
            Assert.Equal(new byte[] { 255, 0, 0, 255 }, result.Pixels.ToArray());
            result = Decode(Payload(1, 1, new ushort[4], new[] { (0, 0) }), 1, 1,
                Image(1, 1, new byte[] { 255, 255, 255, 0 }));
            Assert.Equal(new byte[4], result.Pixels.ToArray());
        }

        [Fact]
        public void OffCanvasLayersCannotOverflowSignedCoordinates()
        {
            byte[] payload = Payload(1, 1, new ushort[] { 0, 65535, 0, 65535 },
                new[] { (int.MinValue, 0), (int.MaxValue, 0), (0, int.MinValue), (0, int.MaxValue) }, true);
            RgbaImage layer = Image(1, 1, new byte[] { 255, 0, 0, 255 });
            RgbaImage result = Decode(payload, 1, 1, layer, layer, layer, layer);
            Assert.Equal(new byte[] { 0, 255, 0, 255 }, result.Pixels.ToArray());
        }

        [Fact]
        public void PayloadBoundariesDimensionsAndReferenceCountsAreChecked()
        {
            RgbaImage layer = Image(1, 1, new byte[] { 255, 0, 0, 255 });
            byte[] payload = Payload(1, 1, new ushort[4], new[] { (0, 0) });
            for (int length = 0; length < payload.Length; length++)
            {
                byte[] truncated = payload.AsSpan(0, length).ToArray();
                Assert.Throws<InvalidDataException>(() => Decode(truncated, 1, 1, layer));
            }
            Assert.Throws<InvalidDataException>(() => Decode(payload, 2, 1, layer));
            Assert.Throws<InvalidDataException>(() => Decode(payload, 1, 1, layer, layer));
            Assert.Throws<InvalidDataException>(() => Decode(HeifTestFile.Join(payload, new byte[1]), 1, 1, layer));
        }

        private static RgbaImage Image(int width, int height, byte[] pixels) =>
            new RgbaImage(width, height, pixels, new ImageColorInformation(1, 13));

        private static RgbaImage Decode(byte[] payload, int width, int height, params RgbaImage[] images)
        {
            HeifItemBuilder builder = new HeifItemBuilder(1) { Type = HeifBox.Code("iovl"), ConstructionMethod = 1 };
            builder.Extents.Add(new ItemExtent(0, (ulong)payload.Length));
            builder.Associations.Add(new PropertyAssociation(0, true));
            uint[] inputs = new uint[images.Length];
            for (int index = 0; index < inputs.Length; index++)
            {
                inputs[index] = (uint)index + 2;
            }
            builder.References.Add(HeifBox.Code("dimg"), inputs);
            HeifItem item = builder.Build();
            HeifBox[] properties =
            {
                new HeifBox(HeifBox.Code("ispe"), 0,
                    HeifTestFile.Join(new byte[4], HeifTestFile.U32((uint)width), HeifTestFile.U32((uint)height)))
            };
            HeifContainer container = new HeifContainer(default, payload, item.Id,
                new Dictionary<uint, HeifItem> { [item.Id] = item }, properties, payload.Length);
            ImageProperties metadata = ImageProperties.Read(container, item, HeicDecodeOptions.Default);
            using PixelBufferScope buffers = new PixelBufferScope(false);
            return OverlayImageDecoder.Decode(container, item, metadata, (id, _) => images[id - 2], buffers);
        }

        private static byte[] Payload(int width, int height, ushort[] fill, (int Left, int Top)[] offsets, bool wide = false)
        {
            using MemoryStream stream = new MemoryStream();
            stream.WriteByte(0);
            stream.WriteByte(wide ? (byte)1 : (byte)0);
            foreach (ushort channel in fill)
            {
                stream.Write(HeifTestFile.U16(channel));
            }
            WriteField(width);
            WriteField(height);
            foreach ((int left, int top) in offsets)
            {
                WriteField(left);
                WriteField(top);
            }
            return stream.ToArray();

            void WriteField(int value)
            {
                if (wide)
                {
                    stream.Write(HeifTestFile.U32(unchecked((uint)value)));
                }
                else
                {
                    stream.Write(HeifTestFile.U16(unchecked((ushort)value)));
                }
            }
        }
    }
}
