using Float = Deterministic.GameFramework.Types.Float;
﻿// SPDX-FileCopyrightText: 2025 Erin Catto
// SPDX-FileCopyrightText: 2025 Ikpil Choi(ikpil@naver.com)
// SPDX-License-Identifier: MIT

namespace Deterministic.GameFramework.Box2D
{
    public struct B2SeparationFunction
    {
        public B2ShapeProxy proxyA;
        public B2ShapeProxy proxyB;
        public B2Sweep sweepA, sweepB;
        public B2Vec2 localPoint;
        public B2Vec2 axis;
        public B2SeparationType type;
    }
}
