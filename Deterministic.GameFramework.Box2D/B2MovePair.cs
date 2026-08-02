using Float = Deterministic.GameFramework.Types.Float;
﻿// SPDX-FileCopyrightText: 2025 Erin Catto
// SPDX-FileCopyrightText: 2025 Ikpil Choi(ikpil@naver.com)
// SPDX-License-Identifier: MIT

namespace Deterministic.GameFramework.Box2D
{
    public class B2MovePair
    {
        public int shapeIndexA;
        public int shapeIndexB;
        public B2MovePair next;
        public bool heap;
    }
}
