// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov (bvdcode) <https://belov.us>

using System;
using System.IO;
using Heic.Net.Hevc;
using Xunit;

namespace Heic.Net.Tests
{
    public class ReferencePictureSyntaxTests
    {
        [Fact]
        public void PredictedSetsRemoveZeroPocAndPreserveNormativeOrder()
        {
            TestBitWriter bits = new TestBitWriter();
            bits.Flag(true);
            bits.Flag(false);
            bits.Ue(0);
            for (int i = 0; i < 3; i++)
            {
                bits.Flag(false);
                bits.Flag(true);
            }
            ShortTermReferenceSet[] sets =
            {
                new ShortTermReferenceSet(new long[] { -1, 1 }), null!
            };
            ShortTermReferenceSet predicted = ShortTermReferenceSet.Read(Reader(bits), sets, 1, false);
            Assert.Equal(new long[] { 1, 2 }, predicted.Deltas);
        }

        [Fact]
        public void SlicePredictionCanSelectEarlierSequenceSet()
        {
            TestBitWriter bits = new TestBitWriter();
            bits.Flag(true);
            bits.Ue(1);
            bits.Flag(true);
            bits.Ue(0);
            bits.Flag(false);
            bits.Flag(true);
            bits.Flag(true);
            ShortTermReferenceSet[] sets =
            {
                new ShortTermReferenceSet(new long[] { -2 }),
                new ShortTermReferenceSet(new long[] { 8, 9 })
            };
            ShortTermReferenceSet predicted = ShortTermReferenceSet.Read(Reader(bits), sets, 2, true);
            Assert.Equal(new long[] { -1, -3 }, predicted.Deltas);
        }

        [Fact]
        public void PredictedSetCannotExceedReferenceLimit()
        {
            TestBitWriter bits = new TestBitWriter();
            bits.Flag(true);
            bits.Flag(true);
            bits.Ue(0);
            for (int i = 0; i < 17; i++)
            {
                bits.Flag(true);
            }
            ShortTermReferenceSet[] sets = { new ShortTermReferenceSet(
                new long[] { -1, -2, -3, -4, -5, -6, -7, -8, -9, -10, -11, -12, -13, -14, -15, -16 }) };
            Assert.Throws<InvalidDataException>(() => ShortTermReferenceSet.Read(Reader(bits), sets, 1, false));
        }

        [Fact]
        public void SelectingAbsentSequenceSetIsRejected()
        {
            TestBitWriter bits = new TestBitWriter();
            bits.Write(0, 4);
            bits.Flag(true);
            ReferencePictureSyntax references = new ReferencePictureSyntax(4, Array.Empty<ShortTermReferenceSet>(), false, 0);
            Assert.Throws<InvalidDataException>(() => references.ReadIntraSlice(Reader(bits)));
        }

        [Fact]
        public void NonPowerOfTwoSequenceSetIndexIsChecked()
        {
            TestBitWriter bits = new TestBitWriter();
            bits.Write(0, 4);
            bits.Flag(true);
            bits.Write(3, 2);
            ShortTermReferenceSet empty = new ShortTermReferenceSet(Array.Empty<long>());
            ReferencePictureSyntax references = new ReferencePictureSyntax(4, new[] { empty, empty, empty }, false, 0);
            Assert.Throws<InvalidDataException>(() => references.ReadIntraSlice(Reader(bits)));
        }

        [Fact]
        public void UnusedLongTermEntriesConsumeTheirCompleteSyntax()
        {
            TestBitWriter bits = new TestBitWriter();
            bits.Write(5, 4);
            bits.Flag(false);
            bits.Ue(0);
            bits.Ue(0);
            bits.Ue(1);
            bits.Ue(1);
            bits.Flag(true);
            bits.Ue(2);
            bits.Write(7, 4);
            bits.Flag(false);
            bits.Flag(true);
            bits.Ue(3);
            bits.Write(13, 4);
            BitReader reader = Reader(bits);
            ReferencePictureSyntax references = new ReferencePictureSyntax(4, Array.Empty<ShortTermReferenceSet>(), true, 1);
            references.ReadIntraSlice(reader);
            Assert.Equal(13, reader.Read(4));
        }

        private static BitReader Reader(TestBitWriter bits) => new BitReader(new ArraySegment<byte>(bits.ToArray()));
    }
}
