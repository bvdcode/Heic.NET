// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov (bvdcode) <https://belov.us>

namespace Heic.Net.Hevc
{
    internal readonly struct WordSampleFormat : ISampleFormat<ushort>
    {
        public int Read(ushort sample) => sample;
        public ushort Write(int sample) => (ushort)sample;
    }
}
