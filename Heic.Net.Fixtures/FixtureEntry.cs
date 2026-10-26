// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

namespace Heic.Net.Fixtures
{
    internal record FixtureEntry(string File, int Width, int Height, string InputSha256,
        string RgbaSha256, int MaxPixels, string[] Features);
}
