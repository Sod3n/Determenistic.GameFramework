namespace Deterministic.GameFramework.Core.Actions;

public class AddSubdomainAction : DARAction<LeafDomain, AddSubdomainAction>
{
    public Guid MatchId { get; set; }
    public LeafDomain Parent { get; set; }
    public LeafDomain Child { get; set; }

    public AddSubdomainAction(LeafDomain parent, LeafDomain child)
    {
        Parent = parent;
        Child = child;
    }

    protected override void ExecuteProcess(LeafDomain domain)
    {

    }
}
