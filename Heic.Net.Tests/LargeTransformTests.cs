// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using System;
using System.Collections.Generic;
using Heic.Net.Hevc;
using Xunit;

namespace Heic.Net.Tests
{
    public class LargeTransformTests
    {
        private const int Guard = 123456789;

        [Theory]
        [InlineData(16, 8)]
        [InlineData(16, 10)]
        [InlineData(32, 8)]
        [InlineData(32, 10)]
        public void EveryFrequencyPreservesClippingRoundingAndScratchBounds(int size, int depth)
        {
            int count = size * size;
            int[] levels = { -32768, -16385, -1025, -129, -128, -127, -65, -64, -63, -1,
                0, 1, 63, 64, 65, 127, 128, 129, 1023, 1024, 16383, 32767 };
            HashSet<int> positions = [];
            for (int frequency = 0; frequency < size; frequency++)
            {
                positions.Add(frequency);
                positions.Add(frequency * size);
                positions.Add(((frequency * size) + size) - 1);
                positions.Add(((size - 1) * size) + frequency);
            }

            using TransformWorkspace workspace = new TransformWorkspace();
            foreach (int position in positions)
            {
                foreach (int level in levels)
                {
                    int[] coefficients = new int[count];
                    coefficients[position] = level;
                    int[] destination = new int[count + 8];
                    Array.Fill(destination, Guard);
                    workspace.Intermediate.Fill(Guard);
                    IntegerTransform.Inverse(coefficients, destination.AsSpan(4, count), size, depth, false, workspace);
                    Assert.Equal(TransformOracle.Apply(coefficients, size, depth, false), destination.AsSpan(4, count).ToArray());
                    Assert.All(destination.AsSpan(0, 4).ToArray(), value => Assert.Equal(Guard, value));
                    Assert.All(destination.AsSpan(count + 4).ToArray(), value => Assert.Equal(Guard, value));
                    Assert.All(workspace.Intermediate.Slice(count).ToArray(), value => Assert.Equal(Guard, value));
                }
            }
        }

        [Theory]
        [InlineData(16, 8)]
        [InlineData(16, 10)]
        [InlineData(32, 8)]
        [InlineData(32, 10)]
        public void PartiallyOverlappingBuffersMatchMatrixOracle(int size, int depth)
        {
            int count = size * size;
            Random random = new Random(31673);
            using TransformWorkspace workspace = new TransformWorkspace();
            foreach (int direction in new[] { -1, 1 })
            {
                int[] storage = new int[count + 7];
                int sourceOffset = direction < 0 ? 7 : 0;
                int destinationOffset = direction < 0 ? 0 : 7;
                for (int index = 0; index < count; index++)
                {
                    storage[sourceOffset + index] = random.Next(short.MinValue, short.MaxValue + 1);
                }
                int[] input = storage.AsSpan(sourceOffset, count).ToArray();
                IntegerTransform.Inverse(storage.AsSpan(sourceOffset, count), storage.AsSpan(destinationOffset, count),
                    size, depth, false, workspace);
                Assert.Equal(TransformOracle.Apply(input, size, depth, false), storage.AsSpan(destinationOffset, count).ToArray());
            }
        }

        [Theory]
        [InlineData(16)]
        [InlineData(32)]
        public void InvalidCoefficientsAreRejectedBeforeDestinationWrites(int size)
        {
            int count = size * size;
            using TransformWorkspace workspace = new TransformWorkspace();
            foreach (int invalid in new[] { short.MinValue - 1, short.MaxValue + 1 })
            {
                int[] coefficients = new int[count];
                coefficients[count - 1] = invalid;
                int[] output = new int[count];
                Array.Fill(output, Guard);
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    IntegerTransform.Inverse(coefficients, output, size, 8, false, workspace));
                Assert.All(output, value => Assert.Equal(Guard, value));
            }
        }
    }
}
