using Heic.Net.Binary;
using System;
using System.Collections.Generic;
using System.IO;

namespace Heic.Net.Hevc
{
    internal class NalPayload
    {
        public NalPayload(ReadOnlyMemory<byte> encoded)
        {
            if (encoded.Length < 2)
            {
                throw new InvalidDataException("Truncated NAL header.");
            }
            int header = (encoded.Span[0] << 8) | encoded.Span[1];
            if ((header & 0x8000) != 0 || (header & 7) == 0)
            {
                throw new InvalidDataException("Invalid NAL header.");
            }

            if (((header >> 3) & 63) != 0)
            {
                throw new UnsupportedHeicFeatureException("multilayer HEVC");
            }
            Type = (header >> 9) & 63;
            byte[] rbsp = new byte[encoded.Length];
            int target = 0;
            int zeros = 0;
            for (int i = 0; i < encoded.Length; i++)
            {
                byte value = encoded.Span[i];
                if (i >= 2 && zeros >= 2 && value == 3)
                {
                    if (i + 1 == encoded.Length || encoded.Span[i + 1] > 3)
                    {
                        throw new InvalidDataException("Invalid emulation-prevention byte.");
                    }

                    zeros = 0;
                    continue;
                }

                rbsp[target++] = value;
                zeros = value == 0 ? zeros + 1 : 0;
            }

            Bytes = rbsp.AsMemory(0, target);
        }

        public int Type { get; }
        public ReadOnlyMemory<byte> Bytes { get; }

        public static IReadOnlyList<NalPayload> ReadLengthPrefixed(ReadOnlyMemory<byte> data, int lengthSize)
        {
            if (lengthSize < 1 || lengthSize > 4)
            {
                throw new InvalidDataException("Invalid NAL length field size.");
            }
            ByteReader reader = new ByteReader(data);
            List<NalPayload> units = new List<NalPayload>();
            while (reader.Remaining > 0)
            {
                ulong length = reader.UInt(lengthSize);
                if (length > (ulong)reader.Remaining)
                {
                    throw new InvalidDataException("NAL length exceeds image item.");
                }
                units.Add(new NalPayload(reader.Bytes(checked((int)length))));
            }

            return units;
        }
    }
}
