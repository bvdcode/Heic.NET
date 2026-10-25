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
            HeifItem item = new HeifItem(1) { BaseOffset = ulong.MaxValue };
            item.Extents.Add(new ItemExtent(1, 1));
            Assert.Throws<InvalidDataException>(() => item.ReadData(new byte[3], default));
            item.BaseOffset = 0;
            item.Extents.Clear();
            item.Extents.Add(new ItemExtent(2, 1));
            item.Extents.Add(new ItemExtent(0, 2));
            Assert.Equal(new byte[] { 3, 1, 2 }, item.ReadData(new byte[] { 1, 2, 3 }, default).ToArray());
        }

        [Fact]
        public void TruncatedFieldFailsInsteadOfReadingPastEnd()
        {
            Assert.Throws<InvalidDataException>(() => new ByteReader(new byte[3]).U32());
            Assert.Throws<InvalidDataException>(() => new ByteReader(new byte[8]).UInt(9));
        }
    }
}
