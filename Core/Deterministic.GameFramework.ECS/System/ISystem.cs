namespace Deterministic.GameFramework.ECS;

public interface ISystem
{
    void Update(EntityWorld state);
}