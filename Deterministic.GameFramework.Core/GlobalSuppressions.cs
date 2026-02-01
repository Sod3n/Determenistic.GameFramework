using System.Diagnostics.CodeAnalysis;

// Suppress deterministic LINQ analyzer warnings for the extension files themselves
// since they are implementing the safe alternatives

[assembly: SuppressMessage("Determinism", "DETERM001:Use deterministic LINQ methods", 
    Justification = "This file implements the deterministic LINQ extensions", 
    Scope = "namespaceanddescendants", 
    Target = "~N:Deterministic.GameFramework.Core.Extensions")]

[assembly: SuppressMessage("Determinism", "DETERM002:Avoid non-deterministic ordering", 
    Justification = "This file implements the deterministic LINQ extensions", 
    Scope = "namespaceanddescendants", 
    Target = "~N:Deterministic.GameFramework.Core.Extensions")]

[assembly: SuppressMessage("Determinism", "DETERM003:HashSet iteration order is not deterministic", 
    Justification = "This file implements the deterministic LINQ extensions", 
    Scope = "namespaceanddescendants", 
    Target = "~N:Deterministic.GameFramework.Core.Extensions")]

[assembly: SuppressMessage("Determinism", "DETERM004:Dictionary iteration order is not deterministic", 
    Justification = "This file implements the deterministic LINQ extensions", 
    Scope = "namespaceanddescendants", 
    Target = "~N:Deterministic.GameFramework.Core.Extensions")]
