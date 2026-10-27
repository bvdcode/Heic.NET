// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov (bvdcode) <https://belov.us>

using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace Heic.Net.Benchmarks
{
    internal static class DecodeMeasurement
    {
        public static async Task<BenchmarkSample> RunAsync(BenchmarkOptions options)
        {
            byte[] input = await File.ReadAllBytesAsync(options.InputFile);
            HeicDecodeOptions limits = new HeicDecodeOptions(maxPixels: options.MaxPixels);
            HeicDocument? document = null;
            switch (options.Mode)
            {
                case BenchmarkMode.Cold:
                    break;
                case BenchmarkMode.Warm:
                    document = HeicDocument.Load(input, limits);
                    Warm(document);
                    GC.Collect();
                    break;
                default:
                    throw new ArgumentException("A child measurement requires an explicit mode.");
            }

            using Process process = Process.GetCurrentProcess();
            TimeSpan cpuBefore = process.TotalProcessorTime;
            long allocatedBefore = GC.GetTotalAllocatedBytes(true);
            long started = Stopwatch.GetTimestamp();
            RgbaImage image;
            switch (options.Mode)
            {
                case BenchmarkMode.Cold:
                    image = HeicDocument.Load(input, limits).DecodePrimary();
                    break;
                case BenchmarkMode.Warm:
                    image = document!.DecodePrimary();
                    break;
                default:
                    throw new ArgumentException("A child measurement requires an explicit mode.");
            }

            double elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            long allocated = GC.GetTotalAllocatedBytes(true) - allocatedBefore;
            process.Refresh();
            double cpu = (process.TotalProcessorTime - cpuBefore).TotalMilliseconds;
            long peak = process.PeakWorkingSet64;
            string hash = Convert.ToHexString(SHA256.HashData(image.Pixels.Span));
            return new BenchmarkSample(options.Mode.Value, options.Repetition, elapsed, cpu, peak,
                allocated, image.Width, image.Height, hash);
        }

        private static void Warm(HeicDocument document)
        {
            RgbaImage image = document.DecodePrimary();
            GC.KeepAlive(image);
        }
    }
}
