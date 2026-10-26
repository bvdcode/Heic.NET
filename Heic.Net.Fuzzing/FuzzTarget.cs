// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using System;
using System.Diagnostics;
using System.IO;

namespace Heic.Net.Fuzzing
{
    internal static class FuzzTarget
    {
        public const int MaxInputBytes = 64 * 1024;
        private static readonly HeicDecodeOptions Limits = new HeicDecodeOptions(
            maxInputBytes: MaxInputBytes, maxPixels: 512 * 512, maxItems: 64, maxDerivationDepth: 8);

        public static bool Exercise(ReadOnlySpan<byte> input, bool decode)
        {
            if (input.Length > MaxInputBytes)
            {
                return false;
            }

            try
            {
                HeicDocument document = HeicDocument.Load(input.ToArray(), Limits);
                if (decode)
                {
                    RgbaImage image = document.DecodePrimary();
                    if ((image.Width <= 0) || (image.Height <= 0) ||
                        (image.Pixels.Length != checked(image.Width * image.Height * 4)))
                    {
                        throw new InvalidOperationException("Decoded image dimensions disagree with its pixel buffer.");
                    }
                }
                return true;
            }
            catch (InvalidDataException exception)
            {
                Trace.WriteLine(exception.Message, "Rejected input");
                return false;
            }
            catch (UnsupportedHeicFeatureException exception)
            {
                Trace.WriteLine(exception.Feature, "Unsupported input");
                return false;
            }
        }
    }
}
