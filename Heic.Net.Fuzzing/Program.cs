// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using System;
using System.IO;
using System.Threading.Tasks;
using SharpFuzz;

namespace Heic.Net.Fuzzing
{
    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            if ((args.Length == 3) && (args[0] == "replay"))
            {
                bool decode = DecodeMode(args[1]);
                byte[] input = await File.ReadAllBytesAsync(args[2]);
                FuzzTarget.Exercise(input, decode);
                return 0;
            }

            if (args.Length == 0)
            {
                bool decode = DecodeMode(Environment.GetEnvironmentVariable("HEIC_FUZZ_TARGET") ?? string.Empty);
                Fuzzer.LibFuzzer.Run(input => { FuzzTarget.Exercise(input, decode); });
                return 0;
            }

            await Console.Error.WriteLineAsync("Usage: set HEIC_FUZZ_TARGET=parse|decode, or replay <parse|decode> <file>");
            return 1;
        }

        private static bool DecodeMode(string mode)
        {
            switch (mode)
            {
                case "parse":
                    return false;
                case "decode":
                    return true;
                default:
                    throw new ArgumentException("The target must be parse or decode.", nameof(mode));
            }
        }
    }
}
