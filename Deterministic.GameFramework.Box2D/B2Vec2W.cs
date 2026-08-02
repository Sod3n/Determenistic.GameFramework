using Float = Deterministic.GameFramework.Types.Float;
﻿// SPDX-FileCopyrightText: 2025 Erin Catto
// SPDX-FileCopyrightText: 2025 Ikpil Choi(ikpil@naver.com)
// SPDX-License-Identifier: MIT

namespace Deterministic.GameFramework.Box2D
{
    // Wide vec2
    public struct B2Vec2W
    {
        public B2FloatW X;
        public B2FloatW Y;

        public B2Vec2W(in B2FloatW X, in B2FloatW Y)
        {
            this.X = X;
            this.Y = Y;
        }
    }
}
