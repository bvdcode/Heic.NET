// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using System;
using System.IO;
using System.Threading.Tasks;

namespace Heic.Net.Fixtures
{
    internal class FixtureCommand
    {
        public async Task<int> RunAsync(string[] args)
        {
            if ((args.Length < 3) || ((args.Length % 2) != 1))
            {
                await Console.Error.WriteLineAsync("Usage: <all|basic|irap> --ffmpeg <executable> [--corpus <source>] [--output <directory>]");
                return 1;
            }
            string? ffmpeg = null;
            string source = Path.GetFullPath("corpus");
            string? output = null;
            for (int i = 1; i < args.Length; i += 2)
            {
                switch (args[i])
                {
                    case "--ffmpeg":
                        ffmpeg = args[i + 1];
                        break;
                    case "--corpus":
                        source = Path.GetFullPath(args[i + 1]);
                        break;
                    case "--output":
                        output = Path.GetFullPath(args[i + 1]);
                        break;
                    default:
                        throw new ArgumentException($"Unknown option: {args[i]}.", nameof(args));
                }
            }
            if (string.IsNullOrWhiteSpace(ffmpeg))
            {
                throw new ArgumentException("An explicit FFmpeg executable is required.", nameof(args));
            }
            output ??= source;
            Directory.CreateDirectory(output);
            NativeCodec codec = new NativeCodec(ffmpeg);
            FixtureManifest manifest = new FixtureManifest(source, output);
            switch (args[0])
            {
                case "all":
                    await new BasicFixtureGenerator(codec, manifest, source).GenerateAsync();
                    await new IrapFixtureGenerator(codec, manifest, output).GenerateAsync();
                    break;
                case "basic":
                    await new BasicFixtureGenerator(codec, manifest, source).GenerateAsync();
                    break;
                case "irap":
                    await new IrapFixtureGenerator(codec, manifest, output).GenerateAsync();
                    break;
                default:
                    throw new ArgumentException("The mode must be all, basic or irap.", nameof(args));
            }
            await manifest.WriteAsync();
            return 0;
        }
    }
}
