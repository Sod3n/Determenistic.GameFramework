using Float = Deterministic.GameFramework.Types.Float;
﻿// SPDX-FileCopyrightText: 2025 Erin Catto
// SPDX-FileCopyrightText: 2025 Ikpil Choi(ikpil@naver.com)
// SPDX-License-Identifier: MIT

namespace Deterministic.GameFramework.Box2D
{
    /// separation = dot(normal, point) - offset
    public struct B2Plane
    {
        public B2Vec2 normal;
        public float offset;

        public B2Plane(B2Vec2 normal, float offset)
        {
            this.normal = normal;
            this.offset = offset;
        }
    }
}
