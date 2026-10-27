// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov (bvdcode) <https://belov.us>

namespace Heic.Net.Hevc
{
    internal readonly struct ByteSampleFormat : ISampleFormat<byte>
    {
        public int Read(byte sample) => sample;
        public byte Write(int sample) => (byte)sample;
    }
}
