using Deterministic.GameFramework.CoreV2;

namespace Deterministic.GameFramework.CoreV2.Example.Components;

[StableId("00000000-0000-0000-0000-000000000108")]
public struct Party : IComponent, IEquatable<Party>
{
    public int PartyId;

    public bool Equals(Party other) => PartyId == other.PartyId;
    public override bool Equals(object? obj) => obj is Party other && Equals(other);
    public override int GetHashCode() => PartyId.GetHashCode();
}
