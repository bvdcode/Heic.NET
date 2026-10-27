// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov (bvdcode) <https://belov.us>

using System;
using System.Collections.Generic;
using System.Text;

namespace Heic.Net.Fixtures
{
    internal static class HeifFixtureWriter
    {
        public static byte[] Write(IReadOnlyList<FixtureItem> items, ushort primary)
        {
            List<byte[]> properties = new List<byte[]>();
            List<byte> associations = new List<byte>(HeifBox.U32(items.Count));
            List<byte> information = new List<byte>(HeifBox.U16(items.Count));
            List<byte> locations = new List<byte>(HeifBox.Join([0x44, 0], HeifBox.U16(items.Count)));
            List<byte> references = new List<byte>();
            List<byte> data = new List<byte>();
            foreach (FixtureItem item in items)
            {
                information.AddRange(HeifBox.Full("infe", 2, item.Hidden ? 1 : 0,
                    HeifBox.U16(item.Identifier), HeifBox.U16(0), Encoding.ASCII.GetBytes(item.Kind), "image\0"u8.ToArray()));
                locations.AddRange(HeifBox.Join(HeifBox.U16(item.Identifier), HeifBox.U16(1),
                    HeifBox.U16(0), HeifBox.U16(1), HeifBox.U32(data.Count), HeifBox.U32(item.Data.Length)));
                data.AddRange(item.Data);
                associations.AddRange(HeifBox.U16(item.Identifier));
                associations.Add(checked((byte)item.Properties.Count));
                foreach (FixtureProperty property in item.Properties)
                {
                    int index = properties.FindIndex(value => value.AsSpan().SequenceEqual(property.Data));
                    if (index < 0)
                    {
                        index = properties.Count;
                        properties.Add(property.Data);
                    }
                    index++;
                    if (index >= 128)
                    {
                        throw new InvalidOperationException("Fixture property count exceeds its 7-bit association field.");
                    }
                    associations.Add((byte)(index | (property.Essential ? 128 : 0)));
                }
                foreach ((string kind, ushort[] targets) in item.References)
                {
                    List<byte> payload = new List<byte>(HeifBox.Join(HeifBox.U16(item.Identifier), HeifBox.U16(targets.Length)));
                    foreach (ushort target in targets)
                    {
                        payload.AddRange(HeifBox.U16(target));
                    }
                    references.AddRange(HeifBox.Box(kind, payload.ToArray()));
                }
            }
            byte[] handler = HeifBox.Full("hdlr", 0, 0, new byte[4], "pict"u8.ToArray(), new byte[12], "Pictures\0"u8.ToArray());
            byte[] metadata = HeifBox.Join(handler, HeifBox.Full("pitm", 0, 0, HeifBox.U16(primary)),
                HeifBox.Full("iinf", 0, 0, information.ToArray()), HeifBox.Full("iloc", 1, 0, locations.ToArray()),
                HeifBox.Box("iprp", HeifBox.Box("ipco", HeifBox.Join(properties.ToArray())), HeifBox.Full("ipma", 0, 0, associations.ToArray())),
                HeifBox.Full("iref", 0, 0, references.ToArray()), HeifBox.Box("idat", data.ToArray()));
            return HeifBox.Join(HeifBox.Box("ftyp", "heic\0\0\0\0mif1heic"u8.ToArray()), HeifBox.Full("meta", 0, 0, metadata));
        }

        public static FixtureProperty Spatial(int width, int height) =>
            new FixtureProperty(HeifBox.Full("ispe", 0, 0, HeifBox.U32(width), HeifBox.U32(height)));

        public static FixtureProperty Color(bool fullRange) =>
            new FixtureProperty(HeifBox.Box("colr", "nclx"u8.ToArray(), HeifBox.U16(1), HeifBox.U16(1), HeifBox.U16(1), [(byte)(fullRange ? 128 : 0)]));

        public static FixtureItem Coded(FixturePattern pattern, ushort identifier = 1, bool hidden = false)
        {
            byte[] depths = pattern.Monochrome ? [(byte)pattern.Depth] : [(byte)pattern.Depth, (byte)pattern.Depth, (byte)pattern.Depth];
            return new FixtureItem(identifier, "hvc1", pattern.ImageData,
            [
                Spatial(64, 48), Color(pattern.Monochrome),
                new FixtureProperty(HeifBox.Box("hvcC", pattern.Configuration), true),
                new FixtureProperty(HeifBox.Full("pixi", 0, 0, [(byte)depths.Length], depths))
            ], hidden);
        }
    }
}
