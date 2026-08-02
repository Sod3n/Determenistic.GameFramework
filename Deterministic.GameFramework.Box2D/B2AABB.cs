using Float = Deterministic.GameFramework.Types.Float;
﻿// SPDX-FileCopyrightText: 2025 Erin Catto
// SPDX-FileCopyrightText: 2025 Ikpil Choi(ikpil@naver.com)
// SPDX-License-Identifier: MIT

namespace Deterministic.GameFramework.Box2D
{
    /// Axis-aligned bounding box
    public struct B2AABB
    {
        public B2Vec2 lowerBound;
        public B2Vec2 upperBound;

        public B2AABB(B2Vec2 lowerBound, B2Vec2 upperBound)
        {
            this.lowerBound = lowerBound;
            this.upperBound = upperBound;
        }
    }
}
