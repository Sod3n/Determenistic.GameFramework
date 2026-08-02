using Float = Deterministic.GameFramework.Types.Float;
﻿// SPDX-FileCopyrightText: 2025 Erin Catto
// SPDX-FileCopyrightText: 2025 Ikpil Choi(ikpil@naver.com)
// SPDX-License-Identifier: MIT

using System;
using System.Runtime.InteropServices;

namespace Deterministic.GameFramework.Box2D
{
    /// Simplex from the GJK algorithm
    [StructLayout(LayoutKind.Sequential)]
    public struct B2Simplex
    {
        public B2SimplexVertex v1;
        public B2SimplexVertex v2;
        public B2SimplexVertex v3; // vertices
        public int count; // number of valid vertices

        public Span<B2SimplexVertex> AsSpan()
        {
            return MemoryMarshal.CreateSpan(ref v1, 3);
        }
    }
}