using Float = Deterministic.GameFramework.Types.Float;
﻿// SPDX-FileCopyrightText: 2025 Erin Catto
// SPDX-FileCopyrightText: 2025 Ikpil Choi(ikpil@naver.com)
// SPDX-License-Identifier: MIT

namespace Deterministic.GameFramework.Box2D
{
    public interface IB2ArenaAllocatable
    {
        int capacity { get; }
        int index { get; }
        int allocation { get; }
        int maxAllocation { get; }

        int Grow();
        void Destroy();
    }
}
