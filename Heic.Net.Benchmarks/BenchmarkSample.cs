// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

namespace Heic.Net.Benchmarks
{
    internal record BenchmarkSample(BenchmarkMode Mode, int Repetition, double WallMilliseconds,
        double CpuMilliseconds, long PeakWorkingSetBytes, long AllocatedBytes, int Width, int Height,
        string OutputSha256, double ProcessMilliseconds = 0);
}
