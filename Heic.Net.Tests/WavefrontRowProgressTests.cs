// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using System;
using System.Threading;
using System.Threading.Tasks;
using Heic.Net.Hevc;
using Xunit;

namespace Heic.Net.Tests
{
    public class WavefrontRowProgressTests
    {
        [Fact]
        public async Task ConsumerWaitsForItsRequiredColumnAfterCoalescedNotifications()
        {
            using WavefrontRowProgress progress = new WavefrontRowProgress();
            progress.Advance(1);
            progress.Advance(2);
            await progress.WaitForAsync(2, CancellationToken.None);
            Task pending = progress.WaitForAsync(3, CancellationToken.None);
            Assert.False(pending.IsCompleted);
            progress.Advance(3);
            await pending.WaitAsync(TimeSpan.FromSeconds(5));
        }

        [Fact]
        public async Task FailureCancellationReleasesAWaitingConsumer()
        {
            using WavefrontRowProgress progress = new WavefrontRowProgress();
            using CancellationTokenSource cancellation = new CancellationTokenSource();
            Task pending = progress.WaitForAsync(2, cancellation.Token);
            await cancellation.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                await pending.WaitAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task PublishedColumnsMakePrecedingWritesVisible()
        {
            const int Count = 1024;
            int[] values = new int[Count];
            using WavefrontRowProgress progress = new WavefrontRowProgress();
            Task producer = Task.Run(() =>
            {
                for (int i = 0; i < Count; i++)
                {
                    values[i] = i + 1;
                    progress.Advance(i + 1);
                }
            });
            for (int i = 0; i < Count; i++)
            {
                await progress.WaitForAsync(i + 1, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
                Assert.Equal(i + 1, values[i]);
            }
            await producer;
        }
    }
}
