// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace Heic.Net.Fixtures
{
    internal static class HeifBox
    {
        public static byte[] Box(string kind, params byte[][] payloads)
        {
            byte[] payload = Join(payloads);
            byte[] result = new byte[checked(payload.Length + 8)];
            BinaryPrimitives.WriteInt32BigEndian(result, result.Length);
            Encoding.ASCII.GetBytes(kind).CopyTo(result, 4);
            payload.CopyTo(result, 8);
            return result;
        }

        public static byte[] Full(string kind, byte version, int flags, params byte[][] payloads) =>
            Box(kind, Join([version, (byte)(flags >> 16), (byte)(flags >> 8), (byte)flags], Join(payloads)));

        public static byte[] U16(int value)
        {
            byte[] data = new byte[2];
            BinaryPrimitives.WriteUInt16BigEndian(data, checked((ushort)value));
            return data;
        }

        public static byte[] U32(int value)
        {
            byte[] data = new byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(data, checked((uint)value));
            return data;
        }

        public static byte[] Join(params byte[][] buffers)
        {
            using MemoryStream stream = new MemoryStream();
            foreach (byte[] buffer in buffers)
            {
                stream.Write(buffer);
            }
            return stream.ToArray();
        }
    }
}
