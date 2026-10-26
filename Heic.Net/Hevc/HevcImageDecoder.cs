// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using System;
using System.Collections.Generic;
using System.IO;

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
            Dictionary<int, SequenceParameters> sequences = new Dictionary<int, SequenceParameters>();
            Dictionary<int, PictureParameters> pictures = new Dictionary<int, PictureParameters>();
            DecodedPicture? result = null;
            bool completed = false;
            try
            {
                foreach (NalPayload unit in configuration.Units)
                {
                    Read(unit);
                }
                foreach (NalPayload unit in NalPayload.ReadLengthPrefixed(data, configuration.LengthSize))
                {
                    Read(unit);
                }

                if (result is null)
                {
                    throw new InvalidDataException("HEVC image contains no intra random-access picture.");
                }

                completed = true;
                return result;
            }
            finally
            {
                if (!completed)
                {
                    result?.Dispose();
                }
            }

            void Read(NalPayload unit)
            {
                switch (unit.Type)
                {
                    case 32:
                        break;
                    case 33:
                        SequenceParameters sequence = SequenceParameters.Read(unit, options);
                        if (!sequences.TryAdd(sequence.Id, sequence))
                        {
                            throw new InvalidDataException("Duplicate HEVC SPS identifier.");
                        }
                        break;
                    case 34:
                        PictureParameters picture = PictureParameters.Read(unit, sequences);
                        if (!pictures.TryAdd(picture.Id, picture))
                        {
                            throw new InvalidDataException("Duplicate HEVC PPS identifier.");
                        }
                        break;
                    case 16:
                    case 17:
                    case 18:
                    case 21:
                    case 19:
                    case 20:
                        if (result is not null)
                        {
                            throw new UnsupportedHeicFeatureException("multiple HEVC pictures or slices per image item");
                        }
                        result = new IntraPictureDecoder(IntraSliceHeader.Read(unit, pictures)).Decode(workerCount);
                        break;
                    case 35:
                    case 36:
                    case 37:
                    case 38:
                    case 39:
                    case 40:
                        break;
                    default:
                        throw new UnsupportedHeicFeatureException("HEVC NAL unit type " + unit.Type);
                }
            }
        }
    }
}
