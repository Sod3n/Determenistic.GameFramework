using Float = Deterministic.GameFramework.Types.Float;
﻿// SPDX-FileCopyrightText: 2025 Erin Catto
// SPDX-FileCopyrightText: 2025 Ikpil Choi(ikpil@naver.com)
// SPDX-License-Identifier: MIT

namespace Deterministic.GameFramework.Box2D
{
    public class B2ChainShape
    {
        public int id;
        public int bodyId;
        public int nextChainId;
        public int count;
        public int materialCount;
        public int[] shapeIndices;
        public B2SurfaceMaterial[] materials;
        public ushort generation;
    }
}
