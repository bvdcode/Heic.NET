// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using System;
using System.IO;
using Heic.Net.Hevc;
using Xunit;

namespace Heic.Net.Tests
{
    public class DecoderConfigurationTests
    {
        [Fact]
        public void ConfigurationOwnsUnescapedParameterUnits()
        {
            byte[] bytes = Configuration(33, new byte[] { 0x42, 1, 0, 0, 3, 1 });
            DecoderConfiguration configuration = DecoderConfiguration.Read(bytes);
            Array.Clear(bytes);
            Assert.Equal(4, configuration.LengthSize);
            Assert.Equal(new byte[] { 0x42, 1, 0, 0, 1 }, Assert.Single(configuration.Units).Bytes.ToArray());
        }

        [Fact]
        public void TruncationAndMismatchedArrayTypesAreRejected()
        {
            byte[] bytes = Configuration(33, new byte[] { 0x42, 1 });
            for (int length = 0; length < bytes.Length; length++)
            {
                Assert.Throws<InvalidDataException>(() => DecoderConfiguration.Read(bytes.AsMemory(0, length)));
            }

            Assert.Throws<InvalidDataException>(() => DecoderConfiguration.Read(Configuration(34, new byte[] { 0x42, 1 })));
            bytes[21] = 2;
            Assert.Throws<InvalidDataException>(() => DecoderConfiguration.Read(bytes));
        }

        private static byte[] Configuration(byte type, byte[] nal)
        {
            byte[] header = new byte[23];
            header[0] = 1;
            header[21] = 3;
            header[22] = 1;
            return HeifTestFile.Join(header, new byte[] { type }, HeifTestFile.U16(1),
                HeifTestFile.U16((ushort)nal.Length), nal);
        }
    }
}
