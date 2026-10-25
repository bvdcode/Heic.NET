// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace Heic.Net.Tests
{
    internal class HeifTestFile
    {
        public static byte[] Minimal(int propertyIndex = 1, uint referenceTarget = 1)
        {
            byte[] information = FullBox("iinf", 0, 0, Join(U16(1),
                FullBox("infe", 2, 0, Join(U16(1), U16(0), Text("hvc1"), new byte[1]))));
            byte[] locations = FullBox("iloc", 1, 0, Join(new byte[] { 0x44, 0 }, U16(1),
                U16(1), U16(1), U16(0), U16(1), U32(0), U32(3)));
            byte[] properties = Box("iprp", Join(Box("ipco", Box("ispe", new byte[12])),
                FullBox("ipma", 0, 0, Join(U32(1), U16(1), new byte[] { 1, (byte)propertyIndex }))));
            byte[] references = FullBox("iref", 0, 0,
                Box("dimg", Join(U16(1), U16(1), U16((ushort)referenceTarget))));
            return Join(Box("ftyp", Join(Text("heic"), U32(0))), FullBox("meta", 0, 0,
                Join(FullBox("pitm", 0, 0, U16(1)), information, locations, properties,
                    references, Box("idat", new byte[] { 7, 8, 9 }))));
        }

        public static byte[] Box(string type, byte[] payload) => Join(U32((uint)payload.Length + 8), Text(type), payload);

        public static byte[] FullBox(string type, byte version, int flags, byte[] payload) => Box(type,
            Join(new byte[] { version, (byte)(flags >> 16), (byte)(flags >> 8), (byte)flags }, payload));

        public static byte[] Text(string value) => Encoding.ASCII.GetBytes(value);

        public static byte[] U16(ushort value)
        {
            byte[] bytes = new byte[2];
            BinaryPrimitives.WriteUInt16BigEndian(bytes, value);
            return bytes;
        }

        public static byte[] U32(uint value)
        {
            byte[] bytes = new byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
            return bytes;
        }

        public static byte[] Join(params byte[][] values)
        {
            using MemoryStream stream = new MemoryStream();
            foreach (byte[] value in values)
            {
                stream.Write(value);
            }
            return stream.ToArray();
        }
    }
}
