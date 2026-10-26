// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using System;
using System.Buffers.Binary;
using System.IO;

namespace Heic.Net.Fixtures
{
    internal static class FixtureReference
    {
        private const int Width = 64;
        private const int Height = 48;
        private const int Scale = 1 << 20;

        public static FixturePixels Convert(byte[] raw, int depth, bool monochrome)
        {
            int sampleBytes = depth == 8 ? 1 : 2;
            int sampleCount = monochrome ? Width * Height : (Width * Height * 3) / 2;
            if (raw.Length != sampleCount * sampleBytes)
            {
                throw new InvalidDataException("FFmpeg output does not match the fixture dimensions and depth.");
            }
            int[] samples = new int[sampleCount];
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] = sampleBytes == 1 ? raw[i] : BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(i * 2));
            }
            int unit = 1 << (depth - 8);
            long lumaFactor = Fixed(255m / (monochrome ? (1 << depth) - 1 : 219 * unit));
            decimal chromaFactor = 255m / (224 * unit);
            const decimal kr = 0.2126m;
            const decimal kb = 0.0722m;
            long redFactor = Fixed(chromaFactor * 2 * (1 - kr));
            long blueFactor = Fixed(chromaFactor * 2 * (1 - kb));
            long greenCb = Fixed((-chromaFactor * 2 * kb * (1 - kb)) / (1 - kr - kb));
            long greenCr = Fixed((-chromaFactor * 2 * kr * (1 - kr)) / (1 - kr - kb));
            byte[] output = new byte[Width * Height * 4];
            int lumaOffset = monochrome ? 0 : 16 * unit;
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    int pixel = (y * Width) + x;
                    long luma = (samples[pixel] - lumaOffset) * lumaFactor;
                    int cb = 0;
                    int cr = 0;
                    if (!monochrome)
                    {
                        int chroma = ((y / 2) * (Width / 2)) + (x / 2);
                        cb = samples[(Width * Height) + chroma] - (1 << (depth - 1));
                        cr = samples[((Width * Height * 5) / 4) + chroma] - (1 << (depth - 1));
                    }
                    output[pixel * 4] = Channel(luma + (cr * redFactor));
                    output[(pixel * 4) + 1] = Channel(luma + (cb * greenCb) + (cr * greenCr));
                    output[(pixel * 4) + 2] = Channel(luma + (cb * blueFactor));
                    output[(pixel * 4) + 3] = 255;
                }
            }
            return new FixturePixels(Width, Height, output);
        }

        private static long Fixed(decimal value) => (long)decimal.Round(value * Scale, 0, MidpointRounding.AwayFromZero);
        private static byte Channel(long value) => (byte)Math.Clamp((value + (Scale / 2)) >> 20, 0L, 255L);
    }
}
