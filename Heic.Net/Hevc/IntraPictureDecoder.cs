// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using System;

namespace Heic.Net.Hevc
{
    /// <summary>
    /// Decodes one independent IDR picture using exclusive plane rentals.
    /// </summary>
    internal class IntraPictureDecoder(IntraSliceHeader slice)
    {
        public DecodedPicture Decode(int workerCount = 1)
        {
            if (workerCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(workerCount));
            }
            SequenceParameters sequence = slice.Picture.Sequence;
            DecodedPicture picture = new DecodedPicture(sequence);
            bool completed = false;
            try
            {
                using CodingBlockMap blocks = new CodingBlockMap(sequence.Width, sequence.Height);
                SaoFilter sao = new SaoFilter(slice);
                int ctbSize = 1 << sequence.Log2Ctb;
                int columns = (sequence.Width + ctbSize - 1) / ctbSize;
                int rows = (sequence.Height + ctbSize - 1) / ctbSize;
                int workers = Math.Min(workerCount, Math.Min(rows, (columns + 1) / 2));
                if (slice.Picture.EntropyCodingSync && workers > 1)
                {
                    new WavefrontDecoder(slice, picture, blocks, sao).DecodeAsync(workers).GetAwaiter().GetResult();
                }
                else
                {
                    DecodeSequential(picture, blocks, sao);
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

        private void DecodeSequential(DecodedPicture picture, CodingBlockMap blocks, SaoFilter sao)
        {
            SequenceParameters sequence = slice.Picture.Sequence;
            IntraCabacReader reader = new IntraCabacReader(slice.Reader, slice.Qp);
            QuantizationGroup quantization = new QuantizationGroup(slice, reader, blocks);
            IntraCodingTreeDecoder tree = new IntraCodingTreeDecoder(slice, reader, blocks, quantization);
            ResidualDecoder residual = new ResidualDecoder(reader, slice.Picture.TransformSkip, slice.Picture.SignDataHiding, sequence.ChromaFormat == 3);
            using ReconstructionWorkspace workspace = new ReconstructionWorkspace();
            BlockReconstructor reconstruction = new BlockReconstructor(picture, residual, slice.Picture, quantization, workspace);
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
                    sao.ReadCtb(x / ctbSize, y / ctbSize, reader);
                    tree.DecodeCtb(x, y, reconstruction);
                }
            }
        }
    }
}
