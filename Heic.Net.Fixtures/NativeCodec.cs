// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov (bvdcode) <https://belov.us>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Heic.Net.Fixtures
{
    internal class NativeCodec(string executable)
    {
        public async Task<byte[]> RunAsync(params string[] arguments)
        {
            ProcessStartInfo settings = new ProcessStartInfo(executable)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            foreach (string argument in arguments)
            {
                settings.ArgumentList.Add(argument);
            }
            using Process process = Process.Start(settings) ?? throw new InvalidOperationException("FFmpeg could not be started.");
            using MemoryStream output = new MemoryStream();
            Task copy = process.StandardOutput.BaseStream.CopyToAsync(output);
            Task<string> errors = process.StandardError.ReadToEndAsync();
            await Task.WhenAll(copy, errors, process.WaitForExitAsync());
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"FFmpeg exited with code {process.ExitCode}: {await errors}");
            }
            return output.ToArray();
        }

        public async Task<FixturePattern> ReadPatternAsync(string path, int depth, bool monochrome)
        {
            List<byte[]> units = ElementaryStream.Read(await File.ReadAllBytesAsync(path));
            byte[][] parameters = units.Where(unit => unit[0] >> 1 is 32 or 33 or 34).ToArray();
            byte[] sequence = parameters.First(unit => (unit[0] >> 1) == 33);
            byte[] unescaped = ElementaryStream.Unescape(sequence);
            byte[] header = HeifBox.Join([1], unescaped.AsSpan(3, 12).ToArray(), [0xf0, 0, 0xfc],
                [(byte)(0xfc | (monochrome ? 0 : 1)), (byte)(0xf8 | (depth - 8)), (byte)(0xf8 | (depth - 8))], [0, 0, 0x0f]);
            List<byte> configuration = new List<byte>(header) { checked((byte)parameters.Length) };
            foreach (byte[] parameter in parameters)
            {
                configuration.Add((byte)(0x80 | (parameter[0] >> 1)));
                configuration.AddRange(HeifBox.Join(HeifBox.U16(1), HeifBox.U16(parameter.Length), parameter));
            }
            List<byte> image = new List<byte>();
            foreach (byte[] unit in units.Where(unit => unit[0] >> 1 is >= 16 and <= 21))
            {
                image.AddRange(HeifBox.U32(unit.Length));
                image.AddRange(unit);
            }
            string pixelFormat = monochrome ? "gray" : depth switch
            {
                8 => "yuv420p",
                10 => "yuv420p10le",
                _ => throw new ArgumentOutOfRangeException(nameof(depth), depth, "Fixtures require 8 or 10 bits.")
            };
            byte[] raw = await RunAsync("-hide_banner", "-loglevel", "error", "-threads", "1", "-i", path,
                "-frames:v", "1", "-pix_fmt", pixelFormat, "-f", "rawvideo", "pipe:1");
            return new FixturePattern(depth, monochrome, configuration.ToArray(), image.ToArray(), FixtureReference.Convert(raw, depth, monochrome));
        }
    }
}
