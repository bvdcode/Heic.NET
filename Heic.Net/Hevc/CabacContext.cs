// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using System;

namespace Heic.Net.Hevc
{
    /// <summary>
    /// Stores one worker-owned CABAC probability state and its most probable symbol.
    /// </summary>
    internal readonly struct CabacContext
    {
        public CabacContext(byte state, bool mostProbableSymbol)
        {
            if (state > 63)
            {
                throw new ArgumentOutOfRangeException(nameof(state));
            }
            State = state;
            MostProbableSymbol = mostProbableSymbol;
        }

        public byte State { get; }
        public bool MostProbableSymbol { get; }

        public static CabacContext Initialize(byte initializationValue, int sliceQp)
        {
            int slope = (initializationValue >> 4) * 5 - 45;
            int intercept = (initializationValue & 15) * 8 - 16;
            int preState = Math.Max(1, Math.Min(126, ((slope * Math.Max(0, Math.Min(51, sliceQp))) >> 4) + intercept));
            return new CabacContext((byte)(preState <= 63 ? 63 - preState : preState - 64), preState > 63);
        }
    }
}
