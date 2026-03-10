using Deterministic.GameFramework.Core.Domain;
using Deterministic.GameFramework.Server;

namespace MultiplayerHelloWorld.Shared;

public class CounterGameState : NetworkGameState
{
    public CounterDomain Counter { get; }
    
    public CounterGameState(Guid matchId) : base(matchId, randomSeed: 0)
    {   
        Counter = new CounterDomain(this);
    }
}

public class CounterDomain : LeafDomain
{
    public int Value { get; set; } = 0;
    
    public CounterDomain(BranchDomain parent) : base(parent)
    {
    }
}
