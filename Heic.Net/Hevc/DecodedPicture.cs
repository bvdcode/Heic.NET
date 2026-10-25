// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using System;

namespace Heic.Net.Hevc
{
    internal class DecodedPicture : IDisposable
    {
        public DecodedPicture(SequenceParameters sequence)
        {
            Sequence = sequence;
            Luma = new SamplePlane(sequence.Width, sequence.Height);
            bool initialized = false;
            try
            {
                if (sequence.ChromaFormat == 1)
                {
                    Cb = new SamplePlane(sequence.Width / 2, sequence.Height / 2);
                    Cr = new SamplePlane(sequence.Width / 2, sequence.Height / 2);
                }

                initialized = true;
            }
            finally
            {
                if (!initialized)
                {
                    Dispose();
                }
            }
        }

        public SequenceParameters Sequence { get; }
        public SamplePlane Luma { get; }
        public SamplePlane? Cb { get; }
        public SamplePlane? Cr { get; }

        public void Dispose()
        {
            Luma.Dispose();
            Cb?.Dispose();
            Cr?.Dispose();
        }
    }
}
