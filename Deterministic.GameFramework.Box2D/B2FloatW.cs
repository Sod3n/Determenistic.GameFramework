using Float = Deterministic.GameFramework.Types.Float;
﻿// SPDX-FileCopyrightText: 2025 Erin Catto
// SPDX-FileCopyrightText: 2025 Ikpil Choi(ikpil@naver.com)
// SPDX-License-Identifier: MIT

using System;
using System.Runtime.InteropServices;

namespace Deterministic.GameFramework.Box2D
{
    // scalar math
    [StructLayout(LayoutKind.Sequential)]
    public struct B2FloatW
    {
        public float X;
        public float Y;
        public float Z;
        public float W;


        public B2FloatW(float x, float y, float z, float w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }

        public ref float this[int index] => ref MemoryMarshal.CreateSpan(ref X, 4)[index];

        public Span<float> AsSpan()
        {
            return MemoryMarshal.CreateSpan(ref X, 4);
        }
    }
}