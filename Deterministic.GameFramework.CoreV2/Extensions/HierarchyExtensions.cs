namespace Deterministic.GameFramework.CoreV2;

public static class HierarchyExtensions
{
    public static void AddChild(this GlobalState state, Entity parent, Entity child)
    {
        ref var parentHierarchy = ref state.GetState<HierarchyComponent>(parent);
        ref var childHierarchy = ref state.GetState<HierarchyComponent>(child);

        childHierarchy.ParentId = parent.Id;

        if (parentHierarchy.FirstChildId == 0)
        {
            parentHierarchy.FirstChildId = child.Id;
        }
        else
        {
            var currentChildId = parentHierarchy.FirstChildId;
            ref var currentChildHierarchy = ref state.GetState<HierarchyComponent>(new Entity(currentChildId));

            while (currentChildHierarchy.NextSiblingId != 0)
            {
                currentChildId = currentChildHierarchy.NextSiblingId;
                currentChildHierarchy = ref state.GetState<HierarchyComponent>(new Entity(currentChildId));
            }

            currentChildHierarchy.NextSiblingId = child.Id;
            childHierarchy.PrevSiblingId = currentChildId;
        }
    }

    public static void RemoveChild(this GlobalState state, Entity parent, Entity child)
    {
        ref var parentHierarchy = ref state.GetState<HierarchyComponent>(parent);
        ref var childHierarchy = ref state.GetState<HierarchyComponent>(child);

        if (childHierarchy.ParentId != parent.Id)
            return;

        if (parentHierarchy.FirstChildId == child.Id)
        {
            parentHierarchy.FirstChildId = childHierarchy.NextSiblingId;
        }
        
        if (childHierarchy.PrevSiblingId != 0)
        {
            ref var prevSibling = ref state.GetState<HierarchyComponent>(new Entity(childHierarchy.PrevSiblingId));
            prevSibling.NextSiblingId = childHierarchy.NextSiblingId;
        }

        if (childHierarchy.NextSiblingId != 0)
        {
            ref var nextSibling = ref state.GetState<HierarchyComponent>(new Entity(childHierarchy.NextSiblingId));
            nextSibling.PrevSiblingId = childHierarchy.PrevSiblingId;
        }

        childHierarchy.ParentId = 0;
        childHierarchy.NextSiblingId = 0;
        childHierarchy.PrevSiblingId = 0;
    }
}
