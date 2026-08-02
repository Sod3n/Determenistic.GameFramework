using Float = Deterministic.GameFramework.Types.Float;
﻿// SPDX-FileCopyrightText: 2025 Erin Catto
// SPDX-FileCopyrightText: 2025 Ikpil Choi(ikpil@naver.com)
// SPDX-License-Identifier: MIT

namespace Deterministic.GameFramework.Box2D
{
    /// World id references a world instance. This should be treated as an opaque handle.
    public readonly struct B2WorldId
    {
        public readonly ushort index1;
        public readonly ushort generation;

        public B2WorldId(ushort index1, ushort generation)
        {
            this.index1 = index1;
            this.generation = generation;
        }
    }
}
