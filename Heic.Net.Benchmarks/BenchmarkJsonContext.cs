// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov (bvdcode) <https://belov.us>

using System.Text.Json.Serialization;

namespace Heic.Net.Benchmarks
{
    [JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
    [JsonSerializable(typeof(BenchmarkSample))]
    [JsonSerializable(typeof(BenchmarkReport))]
    internal partial class BenchmarkJsonContext : JsonSerializerContext
    {
    }
}
