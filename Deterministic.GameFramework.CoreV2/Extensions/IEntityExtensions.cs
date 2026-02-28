namespace Deterministic.GameFramework.CoreV2.Extensions;

public static class IEntityExtensions
{
    public static void Delete(this Entity entity, Context ctx)
    {
        ctx.State.DeleteEntity(entity);
    }
}