namespace Deterministic.GameFramework.Core.Actions;

public class RemoveSubdomainAction : DARAction<LeafDomain, RemoveSubdomainAction>
{
    public Guid MatchId { get; set; }
    public LeafDomain Parent { get; set; }
    public LeafDomain Child { get; set; }

    public RemoveSubdomainAction(LeafDomain parent, LeafDomain child)
    {
        Parent = parent;
        Child = child;
    }

    protected override void ExecuteProcess(LeafDomain domain)
    {
    }
}
