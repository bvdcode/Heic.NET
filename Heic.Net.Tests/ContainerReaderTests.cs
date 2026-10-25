// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using Heic.Net.Binary;
using System;
using System.Buffers.Binary;
using System.IO;
using Heic.Net.Heif;
using Xunit;

namespace Heic.Net.Tests
{
    public class ContainerReaderTests
    {
        [Fact]
        public void BoxCannotEscapeParent()
        {
            byte[] bytes = new byte[8];
            BinaryPrimitives.WriteUInt32BigEndian(bytes, 9);
            Assert.Throws<InvalidDataException>(() => HeifBox.Read(new ByteReader(bytes), 0));
        }

        [Fact]
        public void ExtendedAndZeroLengthBoxesUseParentBounds()
        {
            byte[] extended = new byte[16];
            BinaryPrimitives.WriteUInt32BigEndian(extended, 1);
            BinaryPrimitives.WriteUInt64BigEndian(extended.AsSpan(8), 16);
            Assert.Equal(16, HeifBox.Read(new ByteReader(extended), 0).Offset);
            Assert.Empty(HeifBox.Read(new ByteReader(new byte[8]), 0).Payload.ToArray());
        }

        [Fact]
        public void ExtentsAreCheckedBeforeConversionAndConcatenation()
        {
            HeifItemBuilder item = new HeifItemBuilder(1) { BaseOffset = ulong.MaxValue };
            item.Extents.Add(new ItemExtent(1, 1));
            Assert.Throws<InvalidDataException>(() => item.Build().ReadData(new byte[3], default, 3));
            item.BaseOffset = 0;
            item.Extents.Clear();
            item.Extents.Add(new ItemExtent(2, 1));
            item.Extents.Add(new ItemExtent(0, 2));
            Assert.Equal(new byte[] { 3, 1, 2 }, item.Build().ReadData(new byte[] { 1, 2, 3 }, default, 3).ToArray());
            Assert.Throws<InvalidDataException>(() => item.Build().ReadData(new byte[] { 1, 2, 3 }, default, 2));
        }

        [Fact]
        public void ItemSnapshotDoesNotRetainMutableMetadata()
        {
            HeifItemBuilder builder = new HeifItemBuilder(1) { Type = HeifBox.Code("hvc1") };
            builder.Extents.Add(new ItemExtent(0, 2));
            builder.References.Add(HeifBox.Code("dimg"), new uint[] { 2 });
            HeifItem item = builder.Build();
            builder.Extents.Clear();
            builder.References[HeifBox.Code("dimg")][0] = 3;
            Assert.Single(item.Extents);
            Assert.Equal(2u, item.References[HeifBox.Code("dimg")][0]);
        }

        [Fact]
        public void ItemDataConstructionUsesIdatPayloadCoordinates()
        {
            HeifContainer container = new HeifContainerReader(HeicDecodeOptions.Default).Read(HeifTestFile.Minimal());
            Assert.Equal(1u, container.PrimaryItemId);
            Assert.Equal(new byte[] { 7, 8, 9 }, container.ReadData(1).ToArray());
        }

        [Theory]
        [InlineData(2, 1)]
        [InlineData(1, 2)]
        public void InvalidGraphIndicesFailDuringParsing(int propertyIndex, uint referenceTarget)
        {
            Assert.Throws<InvalidDataException>(() => new HeifContainerReader(HeicDecodeOptions.Default)
                .Read(HeifTestFile.Minimal(propertyIndex, referenceTarget)));
        }

        [Fact]
        public void TruncatedDocumentFailsAtEveryByteBoundary()
        {
            byte[] bytes = HeifTestFile.Minimal();
            for (int length = 0; length < bytes.Length; length++)
            {
                Assert.Throws<InvalidDataException>(() => new HeifContainerReader(HeicDecodeOptions.Default)
                    .Read(bytes.AsMemory(0, length)));
            }
        }

        [Fact]
        public void TruncatedFieldFailsInsteadOfReadingPastEnd()
        {
            Assert.Throws<InvalidDataException>(() => new ByteReader(new byte[3]).U32());
            Assert.Throws<InvalidDataException>(() => new ByteReader(new byte[8]).UInt(9));
        }
    }
}
