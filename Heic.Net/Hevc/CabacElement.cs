// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

namespace Heic.Net.Hevc
{
    internal enum CabacElement
    {
        SplitCodingUnit,
        TransquantBypass,
        PartMode,
        PreviousIntraLuma,
        ChromaMode,
        SplitTransform,
        LumaCbf,
        ChromaCbf,
        QpDelta,
        TransformSkip,
        LastX,
        LastY,
        CodedSubBlock,
        SignificantCoefficient,
        GreaterOne,
        GreaterTwo,
        SaoMerge,
        SaoType
    }
}
