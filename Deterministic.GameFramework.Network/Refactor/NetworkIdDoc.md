Design Document: NetworkId Code Generation & Versioning System
1. Overview & Goals
The goal of this system is to provide a robust, fast, and developer-friendly way to assign and track integer IDs for network messages (ActionService, Reaction, etc.) in the Deterministic Game Framework.

Key Objectives:

Developer Experience (DX): Auto-generate IDs and provide IDE quick-fixes (lightbulbs) to prevent manual ID management.
Backward Compatibility: Track historical IDs via an ids.json file to ensure IDs are never accidentally reused or maliciously changed.
Performance: Eliminate runtime reflection by generating static dictionaries (Type ↔ ID) at compile time.
Seamless Integration: Ensure framework consumers do not have to manually configure tools or edit JSON files.
2. Architecture Components
2.1 The Core Framework
[NetworkId(int id)] Attribute: A simple attribute used to decorate classes.
Base Classes: Reaction<...>, ActionService<...>, etc. Any class inheriting from these must have a [NetworkId].
2.2 The Ledger (ids.json)
A file named NetworkIds.json stored at the root of the consuming project. It acts as the historical ledger. It is tracked in Git to detect changes across branches.

Format:
json
{
  "Deterministic.GameFramework.Network.NetworkState.TickProvider": 1,
  "Deterministic.GameFramework.Network.NetworkState.SetTickRate": 2,
  "Deterministic.GameFramework.Network.NetworkState.DeletedReaction": -3 
}
(Note: Negative values or a separate "deleted" list can represent retired IDs).
2.3 Roslyn Analyzers (Real-time Validation)
Analyzers run continuously in the IDE and enforce rules by analyzing the C# syntax and reading NetworkIds.json (passed via <AdditionalFiles>).

DGF100: Missing NetworkId
Trigger: A class inherits from ActionService or Reaction but lacks [NetworkId].
Severity: Error.
DGF101: Duplicate ID in Source
Trigger: Two classes in the current compilation share the same [NetworkId(x)].
Severity: Error.
DGF102: ID Mismatch / Manual Change
Trigger: The class has [NetworkId(2)], but NetworkIds.json maps this class to 1.
Severity: Error. (Prevents users from manually typing the wrong ID and breaking compatibility).
DGF103: Deleted/Retired ID Reuse
Trigger: The class uses [NetworkId(5)], but 5 is marked as deleted in NetworkIds.json.
Severity: Error.
2.4 Roslyn CodeFix Providers (IDE Quick Actions)
Listens for Analyzer errors and provides (Ctrl+.) or (Alt+Enter) fixes.

Fix for DGF100 (Missing ID): "Generate NetworkId"
Reads NetworkIds.json to find the highest integer MAX.
Injects [NetworkId(MAX + 1)] into the class definition in the C# file.
Fix for DGF102 (ID Mismatch due to Rename/Move): "Acknowledge Type Rename"
If a user renames a class, the Analyzer thinks it's a new class with an old ID.
The CodeFix updates the C# attribute to match the new behavior or prepares a directive to update the JSON.
2.5 Roslyn Source Generator (Runtime Optimization)
Runs during compilation to generate a static registry, bypassing reflection.

Input: All classes with [NetworkId(x)].
Output: Generates NetworkIdRegistry.g.cs:
csharp
public static class NetworkIdRegistry
{
    public static readonly Dictionary<Type, int> TypeToId = new()
    {
        { typeof(TickProvider), 1 },
        { typeof(SetTickRate), 2 }
    };
 
    // Used for deserializing packets from the network
    public static readonly Dictionary<int, Func<INetAction>> IdToFactory = new() ...
}
2.6 MSBuild Post-Build Task (The Ledger Updater)
Because Roslyn Generators cannot safely write to ids.json during IDE design-time, an MSBuild task handles this automatically at the end of a successful build.

Implementation: A custom MSBuild <Target> defined in Deterministic.GameFramework.targets (shipped with your NuGet/Framework).
Trigger: AfterTargets="Build".
Behavior:
Scans the compiled .dll for [NetworkId] attributes using System.Reflection.Metadata (fast, no assembly loading required).
Opens NetworkIds.json.
Adds any new IDs found in the DLL.
If an ID exists in JSON but is missing from the DLL, it marks it as "Deleted" (e.g., -ID or moves it to a "deleted": [] array).
Saves the JSON.
3. Developer Workflow (Step-by-Step)
Creating a new Action: The developer creates public class MoveAction : ActionService<...>.
The Error: The IDE immediately underlines MoveAction in red: "DGF100: Type requires a [NetworkId] attribute".
The Quick Fix: The developer presses Alt+Enter and selects "Generate NetworkId". The IDE modifies the code to [NetworkId(3)] public class MoveAction.
Compilation: The developer hits Build.
Behind the scenes:
The Source Generator creates the NetworkIdRegistry so MoveAction is mapped to 3 in memory.
The build finishes successfully.
The MSBuild Post-Build Task runs silently, sees MoveAction with ID 3, and adds "Namespace.MoveAction": 3 to NetworkIds.json.
Version Control: The developer commits both MoveAction.cs and the updated NetworkIds.json to Git.
4. Implementation Phasing
If you implement this, I recommend the following order:

Phase 1: Create the [NetworkId] attribute and the Source Generator for the runtime mappings.
Phase 2: Create the Analyzer (DGF100) to throw errors when the attribute is missing, and the CodeFix Provider to auto-generate the ID.
Phase 3: Create the MSBuild Post-Build task and NetworkIds.json tracking logic.
Phase 4: Add the advanced Analyzers (DGF101, DGF102, DGF103) that cross-reference the C# code with the JSON file to ensure historical integrity.