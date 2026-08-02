using Float = Deterministic.GameFramework.Types.Float;
﻿// SPDX-FileCopyrightText: 2025 Erin Catto
// SPDX-FileCopyrightText: 2025 Ikpil Choi(ikpil@naver.com)
// SPDX-License-Identifier: MIT

namespace Deterministic.GameFramework.Box2D
{
    public struct B2TreePlane
    {
        public B2AABB leftAABB;
        public B2AABB rightAABB;
        public int leftCount;
        public int rightCount;
    }
}
