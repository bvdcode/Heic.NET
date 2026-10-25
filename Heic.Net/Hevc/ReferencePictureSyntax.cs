// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

namespace Heic.Net.Hevc
{
    internal static class ReferencePictureSyntax
    {
        public static void SkipSets(BitReader reader, int log2Poc)
        {
            int count = HevcSyntax.Ue(reader, 64, "num_short_term_ref_pic_sets");
            int previousCount = 0;
            for (int i = 0; i < count; i++)
            {
                if (i != 0 && reader.ReadFlag())
                {
                    reader.ReadFlag();
                    reader.ReadUev();
                    int currentCount = 0;
                    for (int j = 0; j <= previousCount; j++)
                    {
                        bool used = reader.ReadFlag();
                        bool useDelta = used || reader.ReadFlag();
                        if (useDelta)
                        {
                            currentCount++;
                        }
                    }

                    if (currentCount > 16)
                    {
                        throw new System.IO.InvalidDataException("Too many reference pictures.");
                    }
                    previousCount = currentCount;
                }
                else
                {
                    int negative = HevcSyntax.Ue(reader, 16, "num_negative_pics");
                    int positive = HevcSyntax.Ue(reader, 16 - negative, "num_positive_pics");
                    previousCount = negative + positive;
                    for (int j = 0; j < previousCount; j++)
                    {
                        reader.ReadUev();
                        reader.ReadFlag();
                    }
                }
            }

            if (reader.ReadFlag())
            {
                int longTermCount = HevcSyntax.Ue(reader, 32, "num_long_term_ref_pics_sps");
                for (int i = 0; i < longTermCount; i++)
                {
                    reader.SkipBits(log2Poc + 1);
                }
            }
        }
    }
}
