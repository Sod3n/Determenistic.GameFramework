using Float = Deterministic.GameFramework.Types.Float;
﻿// SPDX-FileCopyrightText: 2025 Ikpil Choi(ikpil@naver.com)
// SPDX-License-Identifier: MIT

using static Deterministic.GameFramework.Box2D.B2Atomics;

namespace Deterministic.GameFramework.Box2D
{
    internal class B2ArenaAllocatorIndexer
    {
        private static B2AtomicInt _indices;

        internal static int Next<T>()
        {
            return b2AtomicFetchAddInt(ref _indices, 1);
        }

        internal static int Index<T>() where T : new()
        {
            return B2ArenaAllocatorIndexer<T>.Index;
        }
    }

    internal class B2ArenaAllocatorIndexer<T> where T : new()
    {
        internal static readonly int Index = B2ArenaAllocatorIndexer.Next<T>();

        private B2ArenaAllocatorIndexer()
        {
        }
    }
}