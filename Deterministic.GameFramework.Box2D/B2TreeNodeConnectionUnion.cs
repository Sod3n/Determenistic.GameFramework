using Float = Deterministic.GameFramework.Types.Float;
﻿// SPDX-FileCopyrightText: 2025 Erin Catto
// SPDX-FileCopyrightText: 2025 Ikpil Choi(ikpil@naver.com)
// SPDX-License-Identifier: MIT

using System.Runtime.InteropServices;

namespace Deterministic.GameFramework.Box2D
{
    [StructLayout(LayoutKind.Explicit)]
    public struct B2TreeNodeConnectionUnion
    {
        [FieldOffset(0)]
        public int parent; // The node parent index (allocated node)

        [FieldOffset(0)]
        public int next; // The node freelist next index (free node)
    }
}