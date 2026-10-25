using Heic.Net.Binary;
using System;
using System.Collections.Generic;
using System.IO;

namespace Heic.Net.Heif
{
    internal partial class HeifContainer
    {
        private readonly HeicDecodeOptions limits;
        private readonly Dictionary<uint, HeifItem> items = new Dictionary<uint, HeifItem>();
        private readonly List<HeifBox> properties = new List<HeifBox>();

        public HeifContainer(ReadOnlyMemory<byte> file, HeicDecodeOptions limits)
        {
            this.limits = limits;
            File = file;
            ByteReader reader = new ByteReader(file);
            bool fileType = false;
            bool meta = false;
            while (reader.Remaining > 0)
            {
                HeifBox box = HeifBox.Read(reader, 0);
                if (box.Type == HeifBox.Code("ftyp"))
                {
                    if (fileType)
                    {
                        throw new InvalidDataException("Duplicate file type box.");
                    }
                    ReadFileType(box);
                    fileType = true;
                }
                else if (box.Type == HeifBox.Code("meta"))
                {
                    if (meta)
                    {
                        throw new InvalidDataException("Duplicate metadata box.");
                    }
                    ReadMeta(box);
                    meta = true;
                }
            }

            if (!fileType || !meta || !items.ContainsKey(PrimaryItemId))
            {
                throw new InvalidDataException("HEIF requires a file type, metadata and primary image item.");
            }
        }

        public ReadOnlyMemory<byte> File { get; }
        public ReadOnlyMemory<byte> ItemData { get; private set; }
        public uint PrimaryItemId { get; private set; }
        public IReadOnlyDictionary<uint, HeifItem> Items => items;
        public IReadOnlyList<HeifBox> Properties => properties;

        private HeifItem Item(uint id)
        {
            if (!items.TryGetValue(id, out HeifItem? item))
            {
                if (items.Count >= limits.MaxItems)
                {
                    throw new InvalidDataException("Item limit exceeded.");
                }
                item = new HeifItem(id);
                items.Add(id, item);
            }

            return item;
        }

        private static void ReadFileType(HeifBox box)
        {
            ByteReader reader = new ByteReader(box.Payload);
            uint major = reader.U32();
            reader.U32();
            bool supported = major == HeifBox.Code("heic") || major == HeifBox.Code("heix");
            while (reader.Remaining > 0)
            {
                uint brand = reader.U32();
                supported |= brand == HeifBox.Code("heic") || brand == HeifBox.Code("heix");
            }

            if (!supported)
            {
                throw new UnsupportedHeicFeatureException("file brands without HEVC images");
            }
        }

        private void ReadMeta(HeifBox box)
        {
            ByteReader reader = new ByteReader(box.Payload);
            if (reader.U32() != 0)
            {
                throw new UnsupportedHeicFeatureException("metadata box version or flags");
            }
            HashSet<uint> seen = new HashSet<uint>();
            while (reader.Remaining > 0)
            {
                HeifBox child = HeifBox.Read(reader, box.Offset);
                if (!seen.Add(child.Type))
                {
                    throw new InvalidDataException("Duplicate metadata child box.");
                }
                string type = TypeName(child.Type);
                switch (type)
                {
                    case "pitm":
                        ByteReader primary = new ByteReader(child.Payload);
                        byte version = Version(primary, 1);
                        PrimaryItemId = version == 0 ? primary.U16() : primary.U32();
                        break;
                    case "iinf":
                        ReadInformation(child);
                        break;
                    case "iloc":
                        ReadLocations(child);
                        break;
                    case "iprp":
                        ReadProperties(child);
                        break;
                    case "iref":
                        ReadReferences(child);
                        break;
                    case "idat":
                        ItemData = child.Payload;
                        break;
                    case "ipro":
                        throw new UnsupportedHeicFeatureException("protected items");
                }
            }
        }

        private static byte Version(ByteReader reader, byte maximum)
        {
            byte version = reader.U8();
            if (version > maximum || reader.UInt(3) != 0)
            {
                throw new UnsupportedHeicFeatureException("box version or flags");
            }

            return version;
        }

        private static string TypeName(uint type) => new string(new[]
        {
            (char)(type >> 24), (char)((type >> 16) & 255), (char)((type >> 8) & 255), (char)(type & 255)
        });
    }
}
