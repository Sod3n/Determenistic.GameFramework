// Run: dotnet run -- [1-7]
using Deterministic.GameFramework.Examples.GettingStarted;
using Deterministic.GameFramework.Examples.Advanced;

var example = args.Length > 0 ? args[0] : "1";

switch (example)
{
    // Getting Started
    case "1": Example01_HelloWorld.Run(); break;
    case "2": Example02_Reactions.Run(); break;
    case "3": Example03_DomainHierarchy.Run(); break;
    case "4": Example04_ObservableAttributes.Run(); break;
    case "5": Example05_NetworkActions.Run(); break;
    // Advanced
    case "6": Example06_ActionInjection.Run(); break;
    case "7": Example07_DuckTyping.Run(); break;
    default: Console.WriteLine("Usage: dotnet run -- [1-7]"); break;
}
