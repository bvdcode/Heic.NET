// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

namespace Heic.Net.Hevc
{
    internal class DecodedPicture
    {
        public DecodedPicture(SequenceParameters sequence)
        {
            Sequence = sequence;
            Luma = new SamplePlane(sequence.Width, sequence.Height);
            if (sequence.ChromaFormat == 1)
            {
                Cb = new SamplePlane(sequence.Width / 2, sequence.Height / 2);
                Cr = new SamplePlane(sequence.Width / 2, sequence.Height / 2);
            }
        }

        public SequenceParameters Sequence { get; }
        public SamplePlane Luma { get; }
        public SamplePlane? Cb { get; }
        public SamplePlane? Cr { get; }
    }
}
