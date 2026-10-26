// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

namespace Heic.Net.Fixtures
{
    internal record FixturePattern(int Depth, bool Monochrome, byte[] Configuration,
        byte[] ImageData, FixturePixels Pixels);
}
