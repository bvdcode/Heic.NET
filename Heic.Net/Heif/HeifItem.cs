using Heic.Net.Binary;
using System;
using System.Collections.Generic;

namespace Heic.Net.Heif
{
    /// <summary>
    /// Stores item metadata assembled while parsing a HEIF document.
    /// </summary>
    /// <remarks>
    /// Descriptors stay internal and must not be mutated after document loading completes.
    /// </remarks>
    internal class HeifItem(uint id)
    {
        public uint Id { get; } = id;
        public uint Type { get; set; }
        public int ConstructionMethod { get; set; }
        public ulong BaseOffset { get; set; }
        public List<ItemExtent> Extents { get; } = new List<ItemExtent>();
        public List<PropertyAssociation> Associations { get; } = new List<PropertyAssociation>();
        public Dictionary<uint, uint[]> References { get; } = new Dictionary<uint, uint[]>();

        public ReadOnlyMemory<byte> ReadData(ReadOnlyMemory<byte> file, ReadOnlyMemory<byte> itemData)
        {
            ReadOnlyMemory<byte> source;
            switch (ConstructionMethod)
            {
                case 0: source = file; break;
                case 1: source = itemData; break;
                default: throw new UnsupportedHeicFeatureException("item construction method " + ConstructionMethod);
            }

            int total = 0;
            foreach (ItemExtent extent in Extents)
            {
                ValidateExtent(source.Length, extent);
                total = checked(total + (int)extent.Length);
            }

            if (Extents.Count == 1)
            {
                return source.Slice(checked((int)(BaseOffset + Extents[0].Offset)), total);
            }

            byte[] result = new byte[total];
            int target = 0;
            foreach (ItemExtent extent in Extents)
            {
                source.Slice((int)(BaseOffset + extent.Offset), (int)extent.Length).CopyTo(result.AsMemory(target));
                target += (int)extent.Length;
            }

            return result;
        }

        private void ValidateExtent(int length, ItemExtent extent)
        {
            if (BaseOffset > (ulong)length || extent.Offset > (ulong)length - BaseOffset ||
                extent.Length > (ulong)length - BaseOffset - extent.Offset)
            {
                throw new System.IO.InvalidDataException("Item extent exceeds its data source.");
            }
        }
    }
}
