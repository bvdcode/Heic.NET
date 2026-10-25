// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using System;
using System.Collections.Generic;
using System.IO;
using Heic.Net.Hevc;

namespace Heic.Net.Heif
{
    /// <summary>
    /// Decodes image items with immutable ancestor paths and read-only document metadata.
    /// </summary>
    internal class ImageItemDecoder(HeifContainer container, IReadOnlyDictionary<uint, ImageProperties> metadata, HeicDecodeOptions options)
    {
        public RgbaImage Decode(uint id) => Decode(id, false, null, ReadOnlyMemory<byte>.Empty, null, true);

        private RgbaImage Decode(uint id, bool alpha, ColorEncoding? inheritedColor, ReadOnlyMemory<byte> inheritedIcc,
            ImageDecodePath? ancestors, bool parallel)
        {
            ImageDecodePath path = new ImageDecodePath(id, ancestors, options.MaxDerivationDepth);
            HeifItem item = container.Items[id];
            if (!metadata.TryGetValue(id, out ImageProperties? properties))
            {
                throw new UnsupportedHeicFeatureException("image item type " + item.Type.ToString("X8"));
            }

            ColorEncoding? color = properties.Color ?? inheritedColor;
            ReadOnlyMemory<byte> icc = properties.IccProfile.IsEmpty ? inheritedIcc : properties.IccProfile;
            RgbaImage result;
            if (item.Type == HeifBox.Code("hvc1"))
            {
                DecoderConfiguration configuration = properties.Configuration ?? throw new InvalidDataException("HEVC image configuration is missing.");
                using DecodedPicture picture = HevcImageDecoder.Decode(configuration, container.ReadData(id), options);
                ValidateDepths(properties.PixelDepths.Span, picture.Sequence);
                result = PictureRenderer.Render(picture, color ?? ColorEncoding.FromVideo(picture.Sequence.Video), icc, alpha);
            }
            else if (item.Type == HeifBox.Code("grid"))
            {
                result = GridImageDecoder.Decode(container, item, properties,
                    target => Decode(target, alpha, color, icc, path, false), parallel);
            }
            else if (item.Type == HeifBox.Code("iden"))
            {
                if (!item.References.TryGetValue(HeifBox.Code("dimg"), out IReadOnlyList<uint>? inputs) || inputs.Count != 1)
                {
                    throw new InvalidDataException("Identity image must reference exactly one input.");
                }

                result = Decode(inputs[0], alpha, color, icc, path, parallel);
            }
            else
            {
                throw new UnsupportedHeicFeatureException("image item type " + item.Type.ToString("X8"));
            }

            if (result.Width != properties.Width || result.Height != properties.Height)
            {
                throw new InvalidDataException("Decoded image disagrees with its spatial extents.");
            }

            foreach (HeifBox transform in properties.Transforms)
            {
                result = ImageTransforms.Apply(result, transform);
            }

            if (!alpha)
            {
                result = ApplyAlpha(item, result, path, parallel);
            }

            return result;
        }

        private RgbaImage ApplyAlpha(HeifItem item, RgbaImage color, ImageDecodePath path, bool parallel)
        {
            if (item.References.ContainsKey(HeifBox.Code("prem")))
            {
                throw new UnsupportedHeicFeatureException("premultiplied alpha images");
            }

            uint? alphaId = null;
            foreach (HeifItem candidate in container.Items.Values)
            {
                if (!candidate.References.TryGetValue(HeifBox.Code("auxl"), out IReadOnlyList<uint>? masters) ||
                    !metadata.TryGetValue(candidate.Id, out ImageProperties? auxiliary) || !ImageProperties.IsAlpha(auxiliary.AuxiliaryType))
                {
                    continue;
                }

                foreach (uint master in masters)
                {
                    if (master != item.Id)
                    {
                        continue;
                    }

                    if (alphaId.HasValue)
                    {
                        throw new UnsupportedHeicFeatureException("multiple alpha auxiliary images");
                    }

                    alphaId = candidate.Id;
                }
            }

            if (!alphaId.HasValue)
            {
                return color;
            }

            RgbaImage alpha = Decode(alphaId.Value, true, null, ReadOnlyMemory<byte>.Empty, path, parallel);
            if (alpha.Width != color.Width || alpha.Height != color.Height)
            {
                throw new InvalidDataException("Alpha image dimensions disagree with the master image.");
            }

            byte[] pixels = color.Pixels.ToArray();
            for (int offset = 3; offset < pixels.Length; offset += 4)
            {
                pixels[offset] = alpha.Pixels.Span[offset - 3];
            }

            return new RgbaImage(color.Width, color.Height, pixels, color.ColorInformation);
        }

        private static void ValidateDepths(ReadOnlySpan<byte> depths, SequenceParameters sequence)
        {
            if (depths.IsEmpty)
            {
                return;
            }

            int components = sequence.ChromaFormat == 0 ? 1 : 3;
            if (depths.Length != components)
            {
                throw new InvalidDataException("Pixel information component count disagrees with the HEVC sequence.");
            }

            foreach (byte depth in depths)
            {
                if (depth != sequence.BitDepth)
                {
                    throw new InvalidDataException("Pixel information depth disagrees with the HEVC sequence.");
                }
            }
        }
    }
}
