using Float = Deterministic.GameFramework.Types.Float;
﻿// SPDX-FileCopyrightText: 2025 Erin Catto
// SPDX-FileCopyrightText: 2025 Ikpil Choi(ikpil@naver.com)
// SPDX-License-Identifier: MIT

namespace Deterministic.GameFramework.Box2D
{
    // Bit set provides fast operations on large arrays of bits.
    public struct B2BitSet
    {
        public ulong[] bits;
        public int blockCapacity;
        public int blockCount;
    }
}
