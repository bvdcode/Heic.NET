// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using System.Collections.Generic;

namespace Heic.Net.Benchmarks
{
    internal record BenchmarkReport(string InputFile, string InputSha256, int MaxPixels, string Runtime,
        string OperatingSystem, string Architecture, int ProcessorCount, bool ServerGc,
        string ColdMeasurement, string WarmMeasurement, string PeakMemoryMeasurement,
        IReadOnlyList<BenchmarkSample> Samples);
}
