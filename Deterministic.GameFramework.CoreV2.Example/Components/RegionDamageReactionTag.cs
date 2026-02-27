using Deterministic.GameFramework.CoreV2;

namespace Deterministic.GameFramework.CoreV2.Example.Components;

// This component acts as a Tag/Subscription for the RegionDamageReaction.
// It effectively "Attaches" the reaction to the entity.
[NetworkId(107)]
public struct RegionDamageReactionTag : IComponent
{
    public Party TargetParty;
}
