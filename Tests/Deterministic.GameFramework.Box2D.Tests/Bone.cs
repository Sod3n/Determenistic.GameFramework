using Float = Deterministic.GameFramework.Types.Float;
﻿// SPDX-FileCopyrightText: 2025 Erin Catto
// SPDX-FileCopyrightText: 2025 Ikpil Choi(ikpil@naver.com)
// SPDX-License-Identifier: MIT

namespace Deterministic.GameFramework.Box2D.Shared
{
    public struct Bone
    {
        public B2BodyId bodyId;
        public B2JointId jointId;
        public float frictionScale;
        public float maxTorque;
        public int parentIndex;
    }
}
