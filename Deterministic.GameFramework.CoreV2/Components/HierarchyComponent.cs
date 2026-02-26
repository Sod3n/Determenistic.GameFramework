namespace Deterministic.GameFramework.CoreV2;

[NetworkId(1)]
public struct HierarchyComponent : IComponent
{
    public int ParentId;
    public int FirstChildId;
    public int NextSiblingId;
    public int PrevSiblingId;
}
