using Float = Deterministic.GameFramework.Types.Float;
﻿// SPDX-FileCopyrightText: 2025 Erin Catto
// SPDX-FileCopyrightText: 2025 Ikpil Choi(ikpil@naver.com)
// SPDX-License-Identifier: MIT

namespace Deterministic.GameFramework.Box2D
{
    public class B2IdPool
    {
        public B2Array<int> freeArray;
        public int nextIndex;

        public void Clear()
        {
            freeArray = new B2Array<int>();
            nextIndex = 0;
        }
    }
}
