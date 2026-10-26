// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using System;
using System.IO;

namespace Heic.Net.Benchmarks
{
    internal record BenchmarkOptions(string InputFile, string OutputDirectory, int Repetitions, int MaxPixels,
        bool IsChild, BenchmarkMode? Mode, int Repetition)
    {
        public static BenchmarkOptions Parse(string[] arguments)
        {
            string? input = null;
            string output = Path.Combine("artifacts", "benchmarks");
            const int DefaultRepetitions = 5;
            int repetitions = DefaultRepetitions;
            int maxPixels = HeicDecodeOptions.Default.MaxPixels;
            bool child = false;
            BenchmarkMode? mode = null;
            int repetition = 0;
            for (int i = 0; i < arguments.Length; i++)
            {
                string argument = arguments[i];
                if (argument == "--child")
                {
                    child = true;
                    continue;
                }

                if (++i >= arguments.Length)
                {
                    throw new ArgumentException("Missing option value: " + argument);
                }

                string value = arguments[i];
                switch (argument)
                {
                    case "--input":
                        input = Path.GetFullPath(value);
                        break;
                    case "--output":
                        output = Path.GetFullPath(value);
                        break;
                    case "--repetitions":
                        repetitions = int.Parse(value);
                        break;
                    case "--max-pixels":
                        maxPixels = int.Parse(value);
                        break;
                    case "--repetition":
                        repetition = int.Parse(value);
                        break;
                    case "--mode":
                        mode = value switch
                        {
                            "cold" => BenchmarkMode.Cold,
                            "warm" => BenchmarkMode.Warm,
                            _ => throw new ArgumentException("Unknown benchmark mode: " + value)
                        };
                        break;
                    default:
                        throw new ArgumentException("Unknown option: " + argument);
                }
            }

            if (input is null || (repetitions <= 0) || (child && (!mode.HasValue || (repetition <= 0))))
            {
                throw new ArgumentException("An input file and positive repetitions are required; child runs also require a mode and repetition.");
            }

            _ = new HeicDecodeOptions(maxPixels: maxPixels);
            return new BenchmarkOptions(input, output, repetitions, maxPixels, child, mode, repetition);
        }
    }
}
