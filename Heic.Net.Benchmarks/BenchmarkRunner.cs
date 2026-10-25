// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;

namespace Heic.Net.Benchmarks
{
    internal static class BenchmarkRunner
    {
        public static async Task RunAsync(string[] arguments)
        {
            if (arguments.Length == 0 || Array.IndexOf(arguments, "--help") >= 0)
            {
                Console.WriteLine("--input <HEIC file> [--repetitions 5] [--output artifacts/benchmarks] [--max-pixels <limit>]");
                return;
            }

            BenchmarkOptions options = BenchmarkOptions.Parse(arguments);
            if (options.IsChild)
            {
                BenchmarkSample sample = await DecodeMeasurement.RunAsync(options);
                Console.WriteLine(JsonSerializer.Serialize(sample, BenchmarkJsonContext.Default.BenchmarkSample));
                return;
            }

            string executable = Environment.ProcessPath ?? throw new InvalidOperationException("The executable path is unavailable.");
            if (Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Run the executable apphost or use dotnet run --project Heic.Net.Benchmarks.");
            }

            Directory.CreateDirectory(options.OutputDirectory);
            string resultFile = Path.Combine(options.OutputDirectory, $"results-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}-{Environment.ProcessId}.json");
            byte[] input = await File.ReadAllBytesAsync(options.InputFile);
            List<BenchmarkSample> samples = new List<BenchmarkSample>();
            BenchmarkReport report = new BenchmarkReport(Path.GetFileName(options.InputFile),
                Convert.ToHexString(SHA256.HashData(input)), options.MaxPixels, RuntimeInformation.FrameworkDescription,
                RuntimeInformation.OSDescription, RuntimeInformation.ProcessArchitecture.ToString(), Environment.ProcessorCount,
                GCSettings.IsServerGC, "First document load and decode in a fresh process; file I/O excluded.",
                "One decode after document load and one warm-up decode; document reused, pixels decoded afresh.",
                "Whole-process peak working set since startup, including warm-up; bytes.", samples.AsReadOnly());
            for (int repetition = 1; repetition <= options.Repetitions; repetition++)
            {
                foreach (BenchmarkMode mode in new[] { BenchmarkMode.Cold, BenchmarkMode.Warm })
                {
                    BenchmarkSample sample = await RunChildAsync(executable, options, mode, repetition);
                    if (samples.Count > 0 && sample.OutputSha256 != samples[0].OutputSha256)
                    {
                        throw new InvalidOperationException("Benchmark repetitions produced different image hashes.");
                    }

                    samples.Add(sample);
                    await File.WriteAllTextAsync(resultFile, JsonSerializer.Serialize(report, BenchmarkJsonContext.Default.BenchmarkReport));
                    Console.WriteLine(FormattableString.Invariant($"{mode} #{repetition}: wall {sample.WallMilliseconds:F2} ms; CPU {sample.CpuMilliseconds:F2} ms; peak {sample.PeakWorkingSetBytes} B; allocated {sample.AllocatedBytes} B"));
                }
            }

            Console.WriteLine(resultFile);
        }

        private static async Task<BenchmarkSample> RunChildAsync(string executable, BenchmarkOptions options, BenchmarkMode mode, int repetition)
        {
            ProcessStartInfo start = new ProcessStartInfo(executable)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            string modeName = mode switch
            {
                BenchmarkMode.Cold => "cold",
                BenchmarkMode.Warm => "warm",
                _ => throw new ArgumentOutOfRangeException(nameof(mode))
            };
            foreach (string argument in new[]
            {
                "--child", "--mode", modeName, "--repetition", repetition.ToString(CultureInfo.InvariantCulture),
                "--input", options.InputFile, "--max-pixels", options.MaxPixels.ToString(CultureInfo.InvariantCulture)
            })
            {
                start.ArgumentList.Add(argument);
            }

            using Process child = new Process { StartInfo = start };
            long started = Stopwatch.GetTimestamp();
            if (!child.Start())
            {
                throw new InvalidOperationException("Cannot start a benchmark child process.");
            }

            Task<string> output = child.StandardOutput.ReadToEndAsync();
            Task<string> error = child.StandardError.ReadToEndAsync();
            await child.WaitForExitAsync();
            double elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            string json = await output;
            string diagnostic = await error;
            if (child.ExitCode != 0)
            {
                throw new InvalidOperationException("Benchmark child failed: " + diagnostic);
            }

            BenchmarkSample sample = JsonSerializer.Deserialize(json, BenchmarkJsonContext.Default.BenchmarkSample)
                ?? throw new InvalidOperationException("Benchmark child returned no measurement.");
            return sample with { ProcessMilliseconds = elapsed };
        }
    }
}
