// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using System;
using System.IO;

namespace Heic.Net.Hevc
{
    /// <summary>
    /// Decodes one independent IDR picture using a fresh worker and exclusive plane rentals.
    /// </summary>
    internal class IntraPictureDecoder
    {
        private readonly IntraSliceHeader slice;
        private readonly IntraCabacReader reader;
        private readonly CodingBlockMap blocks;
        private readonly QuantizationGroup quantization;
        private readonly IntraModeDecoder modes;
        private readonly DecodedPicture picture;
        private readonly SequenceParameters sequence;

        public IntraPictureDecoder(IntraSliceHeader slice)
        {
            this.slice = slice;
            sequence = slice.Picture.Sequence;
            reader = new IntraCabacReader(slice.Reader, slice.Qp);
            blocks = new CodingBlockMap(sequence.Width, sequence.Height);
            quantization = new QuantizationGroup(slice, reader, blocks);
            modes = new IntraModeDecoder(reader, blocks, sequence.Log2Ctb);
            picture = new DecodedPicture(sequence);
        }

        public DecodedPicture Decode()
        {
            bool completed = false;
            try
            {
                SaoFilter sao = new SaoFilter(slice, reader);
                ResidualDecoder residual = new ResidualDecoder(reader, slice.Picture.TransformSkip, slice.Picture.SignDataHiding);
                using BlockReconstructor reconstruction = new BlockReconstructor(picture, residual, slice.Picture, quantization);
                int ctbSize = 1 << sequence.Log2Ctb;
                for (int y = 0; y < sequence.Height; y += ctbSize)
                {
                    if (slice.Picture.EntropyCodingSync && y > 0)
                    {
                        reader.StartWavefrontRow(new BitReader(slice.RowSubstreams[y / ctbSize]), slice.Qp);
                        quantization.StartWavefrontRow();
                    }
                    for (int x = 0; x < sequence.Width; x += ctbSize)
                    {
                        sao.ReadCtb(x / ctbSize, y / ctbSize);
                        CodingTree(x, y, sequence.Log2Ctb, 0, reconstruction);
                        if (slice.Picture.EntropyCodingSync && x == ctbSize)
                        {
                            reader.StoreWavefrontContexts();
                        }
                        bool last = x + ctbSize >= sequence.Width && y + ctbSize >= sequence.Height;
                        if (reader.Termination() != last)
                        {
                            throw new InvalidDataException("Unexpected HEVC slice termination.");
                        }
                        if (slice.Picture.EntropyCodingSync && !last && x + ctbSize >= sequence.Width && !reader.Termination())
                        {
                            throw new InvalidDataException("Missing HEVC row termination.");
                        }
                    }
                }

                if (!slice.DeblockingDisabled)
                {
                    DeblockingFilter.Apply(picture, slice, blocks);
                }
                sao.Apply(picture, blocks);
                completed = true;
                return picture;
            }
            finally
            {
                if (!completed)
                {
                    picture.Dispose();
                }
            }
        }

        private void CodingTree(int x, int y, int log2Size, int depth, BlockReconstructor reconstruction)
        {
            int size = 1 << log2Size;
            bool split = x + size > sequence.Width || y + size > sequence.Height;
            if (!split && log2Size > sequence.Log2MinCodingBlock)
            {
                int context = (blocks.Depth(x - 1, y) > depth ? 1 : 0) + (blocks.Depth(x, y - 1) > depth ? 1 : 0);
                split = reader.Read(CabacElement.SplitCodingUnit, context);
            }

            quantization.Begin(x, y, log2Size);
            if (split)
            {
                int half = size / 2;
                for (int i = 0; i < 4; i++)
                {
                    int cx = x + (i & 1) * half;
                    int cy = y + (i >> 1) * half;
                    if (cx < sequence.Width && cy < sequence.Height)
                    {
                        CodingTree(cx, cy, log2Size - 1, depth + 1, reconstruction);
                    }
                }
            }
            else
            {
                bool bypass = slice.Picture.TransquantBypass && reader.Read(CabacElement.TransquantBypass);
                bool intraSplit = log2Size == sequence.Log2MinCodingBlock && !reader.Read(CabacElement.PartMode);
                blocks.SetCodingUnit(x, y, size, depth, bypass);
                modes.ReadLuma(x, y, size, intraSplit);
                int chromaMode = sequence.ChromaFormat == 0 ? 0 : modes.ReadChroma(blocks.Mode(x, y));
                TransformTree(x, y, x, y, log2Size, 0, 0, intraSplit, chromaMode, false, false, bypass,
                    sequence.MaxTransformDepthIntra + (intraSplit ? 1 : 0), reconstruction);
                quantization.CompleteCodingUnit(x, y, size);
            }
        }

        private void TransformTree(int x, int y, int baseX, int baseY, int log2Size, int depth, int index,
            bool intraSplit, int chromaMode, bool parentCb, bool parentCr, bool bypass, int maxDepth, BlockReconstructor reconstruction)
        {
            bool split = log2Size > sequence.Log2MaxTransform || intraSplit && depth == 0;
            if (!split && log2Size > sequence.Log2MinTransform && depth < maxDepth)
            {
                split = reader.Read(CabacElement.SplitTransform, 5 - log2Size);
            }

            bool cb = parentCb;
            bool cr = parentCr;
            if (sequence.ChromaFormat != 0 && log2Size > 2)
            {
                cb = (depth == 0 || parentCb) && reader.Read(CabacElement.ChromaCbf, depth);
                cr = (depth == 0 || parentCr) && reader.Read(CabacElement.ChromaCbf, depth);
            }

            if (split)
            {
                int half = 1 << (log2Size - 1);
                for (int child = 0; child < 4; child++)
                {
                    TransformTree(x + (child & 1) * half, y + (child >> 1) * half, x, y, log2Size - 1, depth + 1,
                        child, intraSplit, chromaMode, cb, cr, bypass, maxDepth, reconstruction);
                }
            }
            else
            {
                bool luma = reader.Read(CabacElement.LumaCbf, depth == 0 ? 1 : 0);
                if (luma || cb || cr)
                {
                    quantization.ReadDelta();
                }
                blocks.MarkTransform(x, y, 1 << log2Size);
                reconstruction.Reconstruct(x, y, log2Size, 0, blocks.Mode(x, y), luma, bypass);
                if (sequence.ChromaFormat != 0 && (log2Size > 2 || index == 3))
                {
                    int chromaX = (log2Size == 2 ? baseX : x) / 2;
                    int chromaY = (log2Size == 2 ? baseY : y) / 2;
                    int log2Chroma = Math.Max(2, log2Size - 1);
                    reconstruction.Reconstruct(chromaX, chromaY, log2Chroma, 1, chromaMode, cb, bypass);
                    reconstruction.Reconstruct(chromaX, chromaY, log2Chroma, 2, chromaMode, cr, bypass);
                }
            }
        }
    }
}
