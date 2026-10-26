// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace Heic.Net.Browser
{
    [SupportedOSPlatform("browser")]
    public partial class HeicDecoder
    {
        [JSExport]
        public static JSObject Decode(byte[] encoded, int maxPixels)
        {
            HeicDecodeOptions options = HeicDecodeOptions.Default;
            if (maxPixels > 0)
            {
                options = new HeicDecodeOptions(maxPixels: maxPixels);
            }

            HeicDocument document = HeicDocument.Load(encoded, options);
            RgbaImage image = document.DecodePrimary();
            return CreateImage(image.Width, image.Height, MemoryMarshal.AsMemory(image.Pixels).Span);
        }

        [JSImport("createImage", "heic-net")]
        private static partial JSObject CreateImage(int width, int height, [JSMarshalAs<JSType.MemoryView>] Span<byte> pixels);
    }
}
