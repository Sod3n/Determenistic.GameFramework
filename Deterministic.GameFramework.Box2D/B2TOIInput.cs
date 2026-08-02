using Float = Deterministic.GameFramework.Types.Float;
﻿// SPDX-FileCopyrightText: 2025 Erin Catto
// SPDX-FileCopyrightText: 2025 Ikpil Choi(ikpil@naver.com)
// SPDX-License-Identifier: MIT

namespace Deterministic.GameFramework.Box2D
{
    /// Time of impact input
    public struct B2TOIInput
    {
        public B2ShapeProxy proxyA; // The proxy for shape A
        public B2ShapeProxy proxyB; // The proxy for shape B
        public B2Sweep sweepA; // The movement of shape A
        public B2Sweep sweepB; // The movement of shape B
        public float maxFraction; // Defines the sweep interval [0, maxFraction]
    }
}
