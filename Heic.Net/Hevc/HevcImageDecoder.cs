// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov (bvdcode) <https://belov.us>

using System;

namespace Heic.Net.Hevc
{
    /// <summary>
    /// Reconstructs one single-layer intra random-access image from configuration and item NAL units.
    /// </summary>
    /// <remarks>
    /// Single-layer Annex A decoding may discard VPS NAL units under H.265 clause 7.4.3.1.
    /// Reconstruction parameters come from the active SPS and PPS; multilayer NAL headers
    /// and unsupported slice types are rejected before reconstruction.
    /// </remarks>
    internal static class HevcImageDecoder
    {
        public static DecodedPicture Decode(DecoderConfiguration configuration, ReadOnlyMemory<byte> data,
            HeicDecodeOptions options, int workerCount = 1)
        {
            HevcPictureReader reader = new HevcPictureReader(options);
            foreach (NalPayload unit in configuration.Units)
            {
                reader.Read(unit);
            }
            foreach (NalPayload unit in NalPayload.ReadLengthPrefixed(data, configuration.LengthSize))
            {
                reader.Read(unit);
            }
            return reader.Decode(workerCount);
        }
    }
}
