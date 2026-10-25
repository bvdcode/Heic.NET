using System;
using System.Collections.Generic;
using System.IO;

namespace Heic.Net.Hevc
{
    internal static class HevcImageDecoder
    {
        public static DecodedPicture Decode(DecoderConfiguration configuration, ReadOnlyMemory<byte> data,
            HeicDecodeOptions options)
        {
            Dictionary<int, SequenceParameters> sequences = new Dictionary<int, SequenceParameters>();
            Dictionary<int, PictureParameters> pictures = new Dictionary<int, PictureParameters>();
            DecodedPicture? result = null;
            foreach (NalPayload unit in configuration.Units)
            {
                Read(unit);
            }
            foreach (NalPayload unit in NalPayload.ReadLengthPrefixed(data, configuration.LengthSize))
            {
                Read(unit);
            }
            return result ?? throw new InvalidDataException("HEVC image contains no IDR picture.");

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
                    case 19:
                    case 20:
                        if (result is not null)
                        {
                            throw new UnsupportedHeicFeatureException("multiple HEVC pictures or slices per image item");
                        }
                        result = new IntraPictureDecoder(IntraSliceHeader.Read(unit, pictures)).Decode();
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
