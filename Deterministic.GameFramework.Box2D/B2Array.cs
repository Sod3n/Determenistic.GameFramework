using Float = Deterministic.GameFramework.Types.Float;
﻿// SPDX-FileCopyrightText: 2025 Erin Catto
// SPDX-FileCopyrightText: 2025 Ikpil Choi(ikpil@naver.com)
// SPDX-License-Identifier: MIT

namespace Deterministic.GameFramework.Box2D
{
    // Array declaration that doesn't need the type T to be defined
    public struct B2Array<T>
    {
        public T[] data;
        public int count;
        public int capacity;
    }
}
