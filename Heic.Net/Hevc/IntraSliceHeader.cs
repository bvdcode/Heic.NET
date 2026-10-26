// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using System;
using System.Collections.Generic;
using System.IO;

namespace Heic.Net.Hevc
{
    /// <summary>
    /// Describes an independent IDR I slice and positions its reader at the CABAC payload.
    /// </summary>
    internal class IntraSliceHeader
    {
        public PictureParameters Picture { get; private set; } = null!;
        public BitReader Reader { get; private set; } = null!;
        public IReadOnlyList<ArraySegment<byte>> RowSubstreams { get; private set; } = Array.Empty<ArraySegment<byte>>();
        public int Qp { get; private set; }
        public int CbQpOffset { get; private set; }
        public int CrQpOffset { get; private set; }
        public bool SaoLuma { get; private set; }
        public bool SaoChroma { get; private set; }
        public bool DeblockingDisabled { get; private set; }
        public int BetaOffset { get; private set; }
        public int TcOffset { get; private set; }

        public static IntraSliceHeader Read(NalPayload nal, IReadOnlyDictionary<int, PictureParameters> pictures)
        {
            if (nal.Type != 19 && nal.Type != 20)
            {
                throw new UnsupportedHeicFeatureException("HEVC pictures other than IDR");
            }
            BitReader reader = new BitReader(nal.Bytes);
            reader.SkipBits(16);
            if (!reader.ReadFlag())
            {
                throw new UnsupportedHeicFeatureException("multiple HEVC slices per picture");
            }
            reader.ReadFlag();
            int id = HevcSyntax.Ue(reader, 63, "slice_pic_parameter_set_id");
            if (!pictures.TryGetValue(id, out PictureParameters? picture))
            {
                throw new InvalidDataException("Missing picture parameter set.");
            }
            IntraSliceHeader slice = new IntraSliceHeader
            {
                Picture = picture,
                Reader = reader,
                DeblockingDisabled = picture.DeblockingDisabled,
                BetaOffset = picture.BetaOffset,
                TcOffset = picture.TcOffset
            };
            for (int i = 0; i < picture.ExtraHeaderBits; i++)
            {
                if (reader.ReadFlag())
                {
                    throw new UnsupportedHeicFeatureException("nonzero reserved slice header flags");
                }
            }

            if (HevcSyntax.Ue(reader, 2, "slice_type") != 2)
            {
                throw new UnsupportedHeicFeatureException("HEVC inter prediction");
            }
            if (picture.OutputFlagPresent && !reader.ReadFlag())
            {
                throw new UnsupportedHeicFeatureException("HEVC picture output suppression");
            }
            if (picture.Sequence.SaoEnabled)
            {
                slice.SaoLuma = reader.ReadFlag();
                if (picture.Sequence.ChromaFormat != 0)
                {
                    slice.SaoChroma = reader.ReadFlag();
                }
            }

            int depthOffset = 6 * (picture.Sequence.BitDepth - 8);
            slice.Qp = picture.InitialQp + HevcSyntax.Se(reader, -51 - depthOffset, 51 + depthOffset, "slice_qp_delta");
            if (slice.Qp < -depthOffset || slice.Qp > 51)
            {
                throw new InvalidDataException("Invalid slice luma QP.");
            }
            if (picture.SliceChromaQpOffsets)
            {
                slice.CbQpOffset = HevcSyntax.Se(reader, -12, 12, "slice_cb_qp_offset");
                slice.CrQpOffset = HevcSyntax.Se(reader, -12, 12, "slice_cr_qp_offset");
                if (System.Math.Abs(slice.CbQpOffset + picture.CbQpOffset) > 12 || System.Math.Abs(slice.CrQpOffset + picture.CrQpOffset) > 12)
                {
                    throw new InvalidDataException("Combined chroma QP offset is out of range.");
                }
            }

            if (picture.DeblockingOverride && reader.ReadFlag())
            {
                slice.DeblockingDisabled = reader.ReadFlag();
                if (!slice.DeblockingDisabled)
                {
                    slice.BetaOffset = 2 * HevcSyntax.Se(reader, -6, 6, "slice_beta_offset_div2");
                    slice.TcOffset = 2 * HevcSyntax.Se(reader, -6, 6, "slice_tc_offset_div2");
                }
            }

            if (picture.LoopFilterAcrossSlices && (slice.SaoLuma || slice.SaoChroma || !slice.DeblockingDisabled))
            {
                reader.ReadFlag();
            }
            int[] entryLengths = Array.Empty<int>();
            if (picture.EntropyCodingSync)
            {
                int rows = (picture.Sequence.Height - 1) / (1 << picture.Sequence.Log2Ctb) + 1;
                int count = HevcSyntax.Ue(reader, rows - 1, "num_entry_point_offsets");
                if (count != rows - 1)
                {
                    throw new InvalidDataException("WPP entry points do not cover every picture row.");
                }
                entryLengths = new int[count];
                if (count > 0)
                {
                    int bits = HevcSyntax.Ue(reader, 31, "offset_len_minus1") + 1;
                    for (int i = 0; i < count; i++)
                    {
                        uint offset = unchecked((uint)reader.Read(bits));
                        if (offset >= int.MaxValue)
                        {
                            throw new InvalidDataException("HEVC entry point offset exceeds the NAL payload.");
                        }
                        entryLengths[i] = (int)offset + 1;
                    }
                }
            }
            if (picture.HeaderExtension)
            {
                int length = HevcSyntax.Ue(reader, reader.BitsRemaining / 8, "slice_segment_header_extension_length");
                reader.SkipBits(length * 8);
            }

            HevcSyntax.Alignment(reader);
            if (picture.EntropyCodingSync)
            {
                slice.RowSubstreams = nal.Substreams(checked((int)(reader.GetBitPosition() / 8)), entryLengths);
                slice.Reader = new BitReader(slice.RowSubstreams[0]);
            }
            return slice;
        }
    }
}
