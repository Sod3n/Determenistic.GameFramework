using Float = Deterministic.GameFramework.Types.Float;
﻿// SPDX-FileCopyrightText: 2025 Erin Catto
// SPDX-FileCopyrightText: 2025 Ikpil Choi(ikpil@naver.com)
// SPDX-License-Identifier: MIT

using System.Runtime.InteropServices;
using NUnit.Framework;
using static Deterministic.GameFramework.Box2D.B2Tables;

namespace Deterministic.GameFramework.Box2D.Test;

public class B2TableTests
{
    [Test]
    public void Test_B2Tables_b2GetHashSetBytes()
    {
        int size = Marshal.SizeOf<B2SetItem>();
        const int capacity = 1024;
        
        B2HashSet set = b2CreateSet(capacity);
        set.capacity = capacity;
        
        int bytes = b2GetHashSetBytes(ref set);
        Assert.That(bytes, Is.EqualTo(size * capacity));
    }
}
