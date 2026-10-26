// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using System.Collections.Generic;

namespace Heic.Net.Fixtures
{
    internal class FixtureItem(ushort identifier, string kind, byte[] data, List<FixtureProperty> properties, bool hidden = false)
    {
        public ushort Identifier { get; } = identifier;
        public string Kind { get; } = kind;
        public byte[] Data { get; } = data;
        public List<FixtureProperty> Properties { get; } = properties;
        public bool Hidden { get; set; } = hidden;
        public Dictionary<string, ushort[]> References { get; } = new Dictionary<string, ushort[]>();
    }
}
