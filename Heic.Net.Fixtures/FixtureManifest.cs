// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;

namespace Heic.Net.Fixtures
{
    internal class FixtureManifest(string source, string output)
    {
        private const int DefaultMaxPixels = 16 * 1024 * 1024;
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            IndentSize = 2
        };
        private readonly List<FixtureEntry> entries = new List<FixtureEntry>();

        public async Task SaveAsync(string name, IReadOnlyList<FixtureItem> items, ushort primary, FixturePixels pixels, params string[] features)
        {
            byte[] encoded = HeifFixtureWriter.Write(items, primary);
            await File.WriteAllBytesAsync(Path.Combine(output, name), encoded);
            entries.Add(new FixtureEntry(name, pixels.Width, pixels.Height, Hash(encoded), Hash(pixels.Data), DefaultMaxPixels, features));
        }

        public async Task PreserveCameraAsync(string name, int width, int height, string expected)
        {
            byte[] encoded = await File.ReadAllBytesAsync(Path.Combine(source, name));
            entries.Add(new FixtureEntry(name, width, height, Hash(encoded), expected, Math.Max(DefaultMaxPixels, width * height), ["camera", "grid"]));
        }

        public async Task WriteAsync()
        {
            string sourceManifest = Path.Combine(source, "manifest.json");
            List<FixtureEntry> existing = JsonSerializer.Deserialize<List<FixtureEntry>>(await File.ReadAllTextAsync(sourceManifest), JsonOptions)
                ?? throw new InvalidDataException("The corpus manifest must contain a list.");
            HashSet<string> replaced = entries.Select(entry => entry.File).ToHashSet(StringComparer.Ordinal);
            existing.RemoveAll(entry => replaced.Contains(entry.File));
            existing.AddRange(entries);
            await File.WriteAllTextAsync(Path.Combine(output, "manifest.json"), JsonSerializer.Serialize(existing, JsonOptions) + "\n");
            await Console.Out.WriteLineAsync($"Wrote {entries.Count} generated or verified fixture records.");
        }

        private static string Hash(byte[] data) => Convert.ToHexString(SHA256.HashData(data));
    }
}
