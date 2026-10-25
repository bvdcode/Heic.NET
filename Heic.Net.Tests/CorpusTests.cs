// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace Heic.Net.Tests
{
    public class CorpusTests
    {
        public static IEnumerable<object[]> SmallCases() => Cases(false);
        public static IEnumerable<object[]> LargeCases() => Cases(true);

        [Theory]
        [MemberData(nameof(SmallCases))]
        public void CompleteImageMatchesIndependentReference(string file, int width, int height,
            string inputHash, string rgbaHash, int maxPixels) => Verify(file, width, height, inputHash, rgbaHash, maxPixels);

        [Theory]
        [Trait("Category", "LargeImage")]
        [MemberData(nameof(LargeCases))]
        public void LargeCameraImageMatchesIndependentReference(string file, int width, int height,
            string inputHash, string rgbaHash, int maxPixels) => Verify(file, width, height, inputHash, rgbaHash, maxPixels);

        private static void Verify(string file, int width, int height, string inputHash, string rgbaHash, int maxPixels)
        {
            byte[] encoded = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "corpus", file));
            Assert.Equal(inputHash, Convert.ToHexString(SHA256.HashData(encoded)));
            HeicDocument document = HeicDocument.Load(encoded, new HeicDecodeOptions(maxPixels: maxPixels));
            RgbaImage image = document.DecodePrimary();
            Assert.Equal(width, image.Width);
            Assert.Equal(height, image.Height);
            Assert.Equal(rgbaHash, Convert.ToHexString(SHA256.HashData(image.Pixels.Span)));
        }

        private static IEnumerable<object[]> Cases(bool large)
        {
            string path = Path.Combine(AppContext.BaseDirectory, "corpus", "manifest.json");
            using JsonDocument manifest = JsonDocument.Parse(File.ReadAllBytes(path));
            foreach (JsonElement entry in manifest.RootElement.EnumerateArray())
            {
                int maxPixels = entry.GetProperty("maxPixels").GetInt32();
                if ((maxPixels > HeicDecodeOptions.Default.MaxPixels) != large)
                {
                    continue;
                }

                yield return new object[]
                {
                    entry.GetProperty("file").GetString()!, entry.GetProperty("width").GetInt32(),
                    entry.GetProperty("height").GetInt32(), entry.GetProperty("inputSha256").GetString()!,
                    entry.GetProperty("rgbaSha256").GetString()!, maxPixels
                };
            }
        }
    }
}
