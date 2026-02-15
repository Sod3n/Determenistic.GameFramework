using Deterministic.GameFramework.Core;
using Deterministic.GameFramework.Core.Domain;

namespace Deterministic.GameFramework.Examples.Advanced;

/// <summary>
/// Example 9: Tick-Based Game Loop
/// - Fixed tick rate with CurrentTick as the game clock
/// - IRequireTick auto-injects tick info into actions
/// - ScheduleOnTick queues work for a specific future tick
/// - IProcessor checks deadlines each tick
/// - AdvanceToTick replays ticks synchronously
/// </summary>
public static class Example09_TickBasedGameLoop
{
    public static void Run()
    {
        // --- Setup: a root domain with a GameLoop ---
        var root = new RootDomain();
        var gameLoop = new GameLoop(root);
        gameLoop.SetTickRate(10); // 10 ticks per second for readable output

        // --- Create a BombDomain that explodes after a deadline ---
        var bomb = new BombDomain(root);
        Console.WriteLine($"Bomb planted. Fuse = {BombDomain.FuseSeconds}s = {BombDomain.FuseSeconds * gameLoop.TickRate} ticks");

        // --- Arm the bomb using an action (IRequireTick injects CurrentTick + TickRate) ---
        new ArmBombAction().Execute(bomb);
        Console.WriteLine($"Armed at tick {gameLoop.CurrentTick}, deadline = tick {bomb.DeadlineTick.Value}");

        // --- Schedule a defuse attempt at tick 15 (1.5s — too late if fuse is 1s) ---
        gameLoop.ScheduleOnTick(15, () =>
        {
            Console.WriteLine($"[Tick {gameLoop.CurrentTick}] Defuse attempt... {(bomb.Exploded ? "too late!" : "defused!")}");
        });

        // --- Fast-forward the simulation to tick 20 ---
        Console.WriteLine("\n--- Advancing to tick 20 ---");
        gameLoop.AdvanceToTick(20);

        Console.WriteLine($"\nFinal state: tick={gameLoop.CurrentTick}, exploded={bomb.Exploded}");

        root.Dispose();
    }
}

// --- Domain with a tick-based deadline ---

public class BombDomain : BranchDomain, IProcessor
{
    public const int FuseSeconds = 1;
    public ObservableAttribute<long> DeadlineTick { get; } = new();
    public bool Exploded { get; private set; }

    public BombDomain(BranchDomain parent) : base(parent) { }

    public void Process(float delta, long currentTick)
    {
        var deadline = DeadlineTick.Value;
        if (deadline > 0 && !Exploded && currentTick >= deadline)
        {
            Exploded = true;
            Console.WriteLine($"[Tick {currentTick}] BOOM! Bomb exploded.");
        }
    }
}

// --- Action that uses IRequireTick to set a deadline ---

public class ArmBombAction : DARAction<BombDomain, ArmBombAction>, IRequireTick
{
    public long CurrentTick { get; set; }  // Auto-injected by GameLoop reaction
    public int TickRate { get; set; }      // Auto-injected by GameLoop reaction

    protected override void ExecuteProcess(BombDomain bomb)
    {
        bomb.DeadlineTick.Value = CurrentTick + BombDomain.FuseSeconds * TickRate;
    }
}
