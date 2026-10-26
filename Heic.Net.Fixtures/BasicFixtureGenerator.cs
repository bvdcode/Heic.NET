// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Heic.Net.Fixtures
{
    internal class BasicFixtureGenerator(NativeCodec codec, FixtureManifest manifest, string source)
    {
        public async Task GenerateAsync()
        {
            Dictionary<string, FixturePattern> patterns = new Dictionary<string, FixturePattern>();
            foreach ((string key, string stream, int depth, bool mono, string name) in new[]
            {
                ("base", "pattern-8bit.hevc", 8, false, "basic-8bit.heic"),
                ("ten", "pattern-10bit.hevc", 10, false, "basic-10bit.heic"),
                ("mono", "pattern-mono.hevc", 8, true, "monochrome.heic"),
                ("deblock", "deblock-8bit.hevc", 8, false, "deblocking.heic"),
                ("sao", "sao-10bit.hevc", 10, false, "sao.heic"),
                ("scaling", "scaling-8bit.hevc", 8, false, "scaling-lists.heic")
            })
            {
                FixturePattern pattern = await codec.ReadPatternAsync(Path.Combine(source, stream), depth, mono);
                patterns.Add(key, pattern);
                await manifest.SaveAsync(name, [HeifFixtureWriter.Coded(pattern)], 1, pattern.Pixels, key);
            }
            FixturePattern basic = patterns["base"];
            await SaveTransformsAsync(basic);
            FixturePixels canvas = basic.Pixels.Tile(100, 80);
            await manifest.SaveAsync("grid.heic", Grid(basic, 1, 5), 5, canvas, "grid", "edge-crop");
            FixtureItem identity = new FixtureItem(2, "iden", [], [HeifFixtureWriter.Spatial(64, 48), HeifFixtureWriter.Color(false)]);
            identity.References.Add("dimg", [1]);
            await manifest.SaveAsync("identity.heic", [HeifFixtureWriter.Coded(basic, hidden: true), identity], 2, basic.Pixels, "identity");
            await SaveAlphaAsync(basic, patterns["mono"], canvas);
            await manifest.PreserveCameraAsync("20260201_164721.heic", 4000, 3000,
                "CD66C3B43E6A8D1F0319AAE29EC30EF38B85DB54C84855EB1E09D5ED5181F705");
            await manifest.PreserveCameraAsync("20260223_110514.heic", 16320, 12240,
                "6E92D035F8C45558A7BA59D5B3F55B66E7AFD17B554DA89105FECB5F6C213397");
        }

        private async Task SaveTransformsAsync(FixturePattern basic)
        {
            for (int turns = 1; turns <= 3; turns++)
            {
                FixtureItem item = HeifFixtureWriter.Coded(basic);
                item.Properties.Add(Rotation(turns));
                await manifest.SaveAsync($"rotate-{turns * 90}.heic", [item], 1, basic.Pixels.Rotate(turns), "rotation");
            }
            for (int axis = 0; axis <= 1; axis++)
            {
                FixtureItem item = HeifFixtureWriter.Coded(basic);
                item.Properties.Add(new FixtureProperty(HeifBox.Box("imir", [(byte)axis]), true));
                await manifest.SaveAsync($"mirror-{axis}.heic", [item], 1, basic.Pixels.Mirror(axis), "mirror");
            }
            FixtureItem combined = HeifFixtureWriter.Coded(basic);
            combined.Properties.Add(Rotation(1));
            combined.Properties.Add(new FixtureProperty(HeifBox.Box("imir", [0]), true));
            await manifest.SaveAsync("rotate-mirror.heic", [combined], 1, basic.Pixels.Rotate(1).Mirror(0), "rotation", "mirror");
            FixtureItem crop = HeifFixtureWriter.Coded(basic);
            crop.Properties.Add(new FixtureProperty(HeifBox.Box("clap", HeifBox.U32(40), HeifBox.U32(1),
                HeifBox.U32(24), HeifBox.U32(1), HeifBox.U32(0), HeifBox.U32(1), HeifBox.U32(0), HeifBox.U32(1)), true));
            await manifest.SaveAsync("crop.heic", [crop], 1, basic.Pixels.Crop(12, 12, 40, 24), "clean-aperture");
        }

        private async Task SaveAlphaAsync(FixturePattern basic, FixturePattern mono, FixturePixels canvas)
        {
            FixtureProperty auxiliary = new FixtureProperty(HeifBox.Full("auxC", 0, 0,
                "urn:mpeg:mpegB:cicp:systems:auxiliary:alpha\0"u8.ToArray()), true);
            FixtureItem alpha = HeifFixtureWriter.Coded(mono, 2, true);
            alpha.Properties.Add(auxiliary);
            alpha.References.Add("auxl", [1]);
            FixturePixels transparent = basic.Pixels.WithAlpha(mono.Pixels);
            await manifest.SaveAsync("alpha.heic", [HeifFixtureWriter.Coded(basic), alpha], 1, transparent, "alpha");
            FixtureItem master = HeifFixtureWriter.Coded(basic);
            master.Properties.Add(Rotation(1));
            alpha.Properties.Add(Rotation(1));
            await manifest.SaveAsync("alpha-rotated.heic", [master, alpha], 1, transparent.Rotate(1), "alpha", "rotation");
            List<FixtureItem> alphaGrid = Grid(mono, 6, 10);
            FixtureItem alphaCanvas = alphaGrid[^1];
            alphaCanvas.Hidden = true;
            alphaCanvas.Properties.Add(auxiliary);
            alphaCanvas.References.Add("auxl", [5]);
            List<FixtureItem> grid = Grid(basic, 1, 5);
            grid.AddRange(alphaGrid);
            await manifest.SaveAsync("grid-alpha.heic", grid, 5, canvas.WithAlpha(mono.Pixels.Tile(100, 80)), "grid", "alpha");
        }

        private static FixtureProperty Rotation(int turns) => new FixtureProperty(HeifBox.Box("irot", [(byte)turns]), true);

        private static List<FixtureItem> Grid(FixturePattern pattern, ushort first, ushort primary)
        {
            List<FixtureItem> tiles = new List<FixtureItem>();
            ushort[] targets = new ushort[4];
            for (int i = 0; i < targets.Length; i++)
            {
                targets[i] = (ushort)(first + i);
                tiles.Add(HeifFixtureWriter.Coded(pattern, targets[i], true));
            }
            FixtureItem grid = new FixtureItem(primary, "grid", HeifBox.Join([0, 0, 1, 1], HeifBox.U16(100), HeifBox.U16(80)),
                [HeifFixtureWriter.Spatial(100, 80), HeifFixtureWriter.Color(pattern.Monochrome)]);
            grid.References.Add("dimg", targets);
            tiles.Add(grid);
            return tiles;
        }
    }
}
