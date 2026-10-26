// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using System.Threading.Tasks;

namespace Heic.Net.Fixtures
{
    internal static class Program
    {
        private static Task<int> Main(string[] args) => new FixtureCommand().RunAsync(args);
    }
}
