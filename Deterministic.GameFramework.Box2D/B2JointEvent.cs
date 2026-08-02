using Float = Deterministic.GameFramework.Types.Float;
﻿// SPDX-FileCopyrightText: 2025 Erin Catto
// SPDX-FileCopyrightText: 2025 Ikpil Choi(ikpil@naver.com)
// SPDX-License-Identifier: MIT

namespace Deterministic.GameFramework.Box2D
{
    /// Joint events report joints that are awake and have a force and/or torque exceeding the threshold
    /// The observed forces and torques are not returned for efficiency reasons.
    public struct B2JointEvent
    {
        /// The joint id
        public B2JointId jointId;

        /// The user data from the joint for convenience
        public B2UserData userData;
    }
}
