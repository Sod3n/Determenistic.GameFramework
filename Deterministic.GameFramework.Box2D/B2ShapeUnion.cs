using Float = Deterministic.GameFramework.Types.Float;
﻿// SPDX-FileCopyrightText: 2025 Erin Catto
// SPDX-FileCopyrightText: 2025 Ikpil Choi(ikpil@naver.com)
// SPDX-License-Identifier: MIT

using System.Runtime.InteropServices;

namespace Deterministic.GameFramework.Box2D
{
    [StructLayout(LayoutKind.Explicit)]
    public struct B2ShapeUnion
    {
        [FieldOffset(0)]
        public B2Capsule capsule;

        [FieldOffset(0)]
        public B2Circle circle;

        [FieldOffset(0)]
        public B2Polygon polygon;

        [FieldOffset(0)]
        public B2Segment segment;

        [FieldOffset(0)]
        public B2ChainSegment chainSegment;
    }
}
