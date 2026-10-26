// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using System;
using System.IO;
using Heic.Net.Fuzzing;
using Xunit;

namespace Heic.Net.Tests
{
    public class FuzzTargetTests
    {
        [Theory]
        [InlineData("basic-8bit.heic")]
        [InlineData("basic-10bit.heic")]
        [InlineData("grid-alpha.heic")]
        public void ValidSeedsReachBothTargets(string name)
        {
            byte[] input = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "corpus", name));
            Assert.True(FuzzTarget.Exercise(input, false));
            Assert.True(FuzzTarget.Exercise(input, true));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void TruncationsAreExpectedRejections(bool decode)
        {
            byte[] input = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "corpus", "basic-8bit.heic"));
            for (int length = 0; length < input.Length; length++)
            {
                Assert.False(FuzzTarget.Exercise(input.AsSpan(0, length), decode));
            }
        }

        [Fact]
        public void OversizedInputIsRejectedBeforeParsing()
        {
            Assert.False(FuzzTarget.Exercise(new byte[FuzzTarget.MaxInputBytes + 1], true));
        }
    }
}
