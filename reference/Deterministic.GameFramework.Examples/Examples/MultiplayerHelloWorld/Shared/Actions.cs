using Newtonsoft.Json;
using Deterministic.GameFramework.Server;

namespace MultiplayerHelloWorld.Shared;

public class IncrementAction : NetworkAction<CounterGameState, IncrementAction>
{
    [JsonProperty] public int Amount { get; set; } = 1;
    
    protected override void ExecuteProcess(CounterGameState game)
    {
        game.Counter.Value += Amount;
        Console.WriteLine($"Counter incremented by {Amount}. New value: {game.Counter.Value}");
    }
}
