using Float = Deterministic.GameFramework.Types.Float;
﻿// SPDX-FileCopyrightText: 2025 Erin Catto
// SPDX-FileCopyrightText: 2025 Ikpil Choi(ikpil@naver.com)
// SPDX-License-Identifier: MIT

using System.Collections.Generic;

namespace Deterministic.GameFramework.Box2D
{
    public class B2ShapeRefComparer : IComparer<B2Visitor>
    {
        internal static readonly B2ShapeRefComparer Shared = new B2ShapeRefComparer();

        private B2ShapeRefComparer()
        {
        }

        public int Compare(B2Visitor a, B2Visitor b)
        {
            return B2Sensors.b2CompareVisitors(ref a, ref b);
        }
    }
}
