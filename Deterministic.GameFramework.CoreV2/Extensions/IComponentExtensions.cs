namespace Deterministic.GameFramework.CoreV2.Extensions;

public static class IComponentExtensions
{
    public static T AddComponent<T>(this IComponent entity, T component, Context ctx) where T : struct, IComponent
    {
        ctx.State.AddComponent(ctx.Entity, component);
        return component;
    }
    
    public static T GetComponent<T>(this IComponent entity, Context ctx) where T : struct, IComponent
    {
        return ctx.State.GetComponent<T>(ctx.Entity);
    }
    
    public static T? TryGetComponent<T>(this IComponent component, Context ctx) where T : struct, IComponent
    {
        return ctx.State.TryGetComponent<T>(ctx.Entity);
    }
    
    public static bool HasComponent<T>(this IComponent entity, Context ctx) where T : struct, IComponent
    {
        return ctx.State.HasComponent<T>(ctx.Entity);
    }
    
    public static void RemoveComponent<T>(this IComponent entity, Context ctx) where T : struct, IComponent
    {
        ctx.State.RemoveComponent<T>(ctx.Entity);
    }
    
    public static T AddComponent<T>(this Entity entity, T component, Context ctx) where T : struct, IComponent
    {
        ctx.State.AddComponent(entity, component);
        return component;
    }
    
    public static T GetComponent<T>(this Entity entity, Context ctx) where T : struct, IComponent
    {
        return ctx.State.GetComponent<T>(entity);
    }
    
    public static T? TryGetComponent<T>(this Entity entity, Context ctx) where T : struct, IComponent
    {
        return ctx.State.TryGetComponent<T>(entity);
    }
    
    public static bool HasComponent<T>(this Entity entity, Context ctx) where T : struct, IComponent
    {
        return ctx.State.HasComponent<T>(entity);
    }
    
    public static void RemoveComponent<T>(this Entity entity, Context ctx) where T : struct, IComponent
    {
        ctx.State.RemoveComponent<T>(entity);
    }
    
    public static T AddComponent<T>(this Context ctx, T component) where T : struct, IComponent
    {
        ctx.State.AddComponent(ctx.Entity, component);
        return component;
    }
    
    public static T GetComponent<T>(this Context ctx) where T : struct, IComponent
    {
        return ctx.State.GetComponent<T>(ctx.Entity);
    }
    
    public static T? TryGetComponent<T>(this Context ctx) where T : struct, IComponent
    {
        return ctx.State.TryGetComponent<T>(ctx.Entity);
    }
    
    public static bool HasComponent<T>(this Context ctx) where T : struct, IComponent
    {
        return ctx.State.HasComponent<T>(ctx.Entity);
    }
    
    public static void RemoveComponent<T>(this Context ctx) where T : struct, IComponent
    {
        ctx.State.RemoveComponent<T>(ctx.Entity);
    }
}