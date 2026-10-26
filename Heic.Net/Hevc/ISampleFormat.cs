// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

namespace Heic.Net.Hevc
{
    internal interface ISampleFormat<TSample> where TSample : unmanaged
    {
        int Read(TSample sample);
        TSample Write(int sample);
    }
}
