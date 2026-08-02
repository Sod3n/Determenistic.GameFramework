using Float = Deterministic.GameFramework.Types.Float;
﻿// SPDX-FileCopyrightText: 2025 Erin Catto
// SPDX-FileCopyrightText: 2025 Ikpil Choi(ikpil@naver.com)
// SPDX-License-Identifier: MIT

using System;

namespace Deterministic.GameFramework.Box2D
{
    [Flags]
    public enum B2TreeNodeFlags
    {
        b2_allocatedNode = 0x0001,
        b2_enlargedNode = 0x0002,
        b2_leafNode = 0x0004,
    };
}
