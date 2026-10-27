// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov (bvdcode) <https://belov.us>

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Heic.Net.Fixtures
{
    internal class IrapFixtureGenerator(NativeCodec codec, FixtureManifest manifest, string output)
    {
        public async Task GenerateAsync()
        {
            string temporary = Directory.CreateTempSubdirectory("heic-fixtures-").FullName;
            try
            {
                foreach (int slices in new[] { 1, 2, 3 })
                {
                    foreach (int depth in new[] { 8, 10 })
                    {
                        await GenerateAsync(temporary, depth, slices);
                    }
                }
            }
            finally
            {
                Directory.Delete(temporary, true);
            }
        }

        private async Task GenerateAsync(string temporary, int depth, int slices)
        {
            string stream = Path.Combine(temporary, "open-gop.hevc");
            string pixelFormat = depth switch
            {
                8 => "yuv420p",
                10 => "yuv420p10le",
                _ => throw new ArgumentOutOfRangeException(nameof(depth), depth, "Fixtures require 8 or 10 bits.")
            };
            string parameters = "keyint=2:min-keyint=2:scenecut=0:open-gop=1:bframes=0:pools=1:frame-threads=1:repeat-headers=1";
            if (slices > 1)
            {
                parameters += $":slices={slices}:ctu=16:wpp=1";
            }
            await codec.RunAsync("-hide_banner", "-loglevel", "error", "-f", "lavfi", "-i", "testsrc2=size=64x48:rate=1",
                "-frames:v", "5", "-pix_fmt", pixelFormat, "-c:v", "libx265", "-x265-params", parameters, "-y", stream);
            List<byte[]> units = ElementaryStream.Read(await File.ReadAllBytesAsync(stream));
            Dictionary<int, byte[]> sets = new Dictionary<int, byte[]>();
            List<byte[]> cra = new List<byte[]>();
            foreach (byte[] unit in units)
            {
                int kind = unit[0] >> 1;
                if (kind is 32 or 33 or 34)
                {
                    sets[kind] = unit;
                }
                if (kind == 21)
                {
                    cra.Add(unit);
                }
                else if (cra.Count != 0)
                {
                    break;
                }
            }
            if (cra.Count == 0)
            {
                throw new InvalidDataException("The encoded stream contains no CRA picture.");
            }
            foreach ((string name, int kind) in new[] { ("cra", 21), ("bla-w-lp", 16), ("bla-w-radl", 17), ("bla-n-lp", 18) })
            {
                string prefix = slices > 1 ? $"slices{slices}-" : string.Empty;
                string stem = $"{prefix}{name}-{depth}bit";
                List<byte> encoded = new List<byte>();
                foreach (int key in new[] { 32, 33, 34 })
                {
                    encoded.AddRange(HeifBox.Join([0, 0, 0, 1], sets[key]));
                }
                foreach (byte[] unit in cra)
                {
                    encoded.AddRange(HeifBox.Join([0, 0, 0, 1, (byte)((unit[0] & 1) | (kind << 1))], unit.AsSpan(1).ToArray()));
                }
                string elementary = Path.Combine(output, stem + ".hevc");
                await File.WriteAllBytesAsync(elementary, encoded.ToArray());
                FixturePattern pattern = await codec.ReadPatternAsync(elementary, depth, false);
                await manifest.SaveAsync(stem + ".heic", [HeifFixtureWriter.Coded(pattern)], 1, pattern.Pixels, name, $"{depth}bit", $"{slices}-slices");
            }
        }
    }
}
