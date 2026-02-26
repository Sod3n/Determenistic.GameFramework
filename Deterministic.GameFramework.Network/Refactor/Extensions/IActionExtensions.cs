using Deterministic.GameFramework.Network.NetworkState;

namespace Deterministic.GameFramework.Network.Refactor.Extensions;

public static class IActionExtensions
{
    public static void Execute<TAction>(this TAction action, Context ctx) where TAction : struct, IAction 
        => ctx.World.Execute(action, ctx.Entity);
}