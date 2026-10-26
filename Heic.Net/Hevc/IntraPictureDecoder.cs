// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using System;

namespace Heic.Net.Hevc
{
    /// <summary>
    /// Decodes one independent intra random-access picture using exclusive plane rentals.
    /// </summary>
    internal class IntraPictureDecoder(IntraSliceHeader slice, PicturePartitions? partitions = null)
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
                using CodingBlockMap blocks = new CodingBlockMap(sequence.Width, sequence.Height, partitions);
                SaoFilter sao = new SaoFilter(slice, partitions);
                int ctbSize = 1 << sequence.Log2Ctb;
                int columns = (sequence.Width + ctbSize - 1) / ctbSize;
                int rows = (sequence.Height + ctbSize - 1) / ctbSize;
                int workers = Math.Min(workerCount, Math.Min(rows, (columns + 1) / 2));
                if (partitions is null && slice.Picture.EntropyCodingSync && workers > 1)
                {
                    new WavefrontDecoder(slice, picture, blocks, sao).DecodeAsync(workers).GetAwaiter().GetResult();
                }
                else
                {
                    if (partitions is null)
                    {
                        DecodeSequential(slice, picture, blocks, sao);
                    }
                    else
                    {
                        foreach (IntraSliceHeader segment in partitions.Slices)
                        {
                            DecodeSequential(segment, picture, blocks, sao);
                        }
                    }
                }

                if (partitions is not null || !slice.DeblockingDisabled)
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

        private void DecodeSequential(IntraSliceHeader slice, DecodedPicture picture, CodingBlockMap blocks, SaoFilter sao)
        {
            SequenceParameters sequence = slice.Picture.Sequence;
            IntraCabacReader reader = new IntraCabacReader(slice.Reader, slice.Qp);
            QuantizationGroup quantization = new QuantizationGroup(slice, reader, blocks);
            IntraCodingTreeDecoder tree = new IntraCodingTreeDecoder(slice, reader, blocks, quantization);
            ResidualDecoder residual = new ResidualDecoder(reader, slice.Picture.TransformSkip, slice.Picture.SignDataHiding, sequence.ChromaFormat == 3);
            using ReconstructionWorkspace workspace = new ReconstructionWorkspace();
            BlockReconstructor reconstruction = new BlockReconstructor(picture, residual, slice.Picture, quantization, workspace, partitions);
            int ctbSize = 1 << sequence.Log2Ctb;
            int columns = (sequence.Width + ctbSize - 1) / ctbSize;
            int firstRow = slice.StartCtbAddress / columns;
            for (int address = slice.StartCtbAddress; address < slice.EndCtbAddress; address++)
            {
                int column = address % columns;
                int row = address / columns;
                if (slice.Picture.EntropyCodingSync && column == 0 && address != slice.StartCtbAddress)
                {
                    reader.StartWavefrontRow(new BitReader(slice.RowSubstreams[row - firstRow]), slice.Qp);
                    quantization.StartWavefrontRow();
                }
                sao.ReadCtb(column, row, reader);
                tree.DecodeCtb(column * ctbSize, row * ctbSize, reconstruction, address + 1 == slice.EndCtbAddress);
            }
        }
    }
}
