using System;

namespace Deterministic.GameFramework.Core;

/// <summary>
/// Base interface for all reactions. All reactions must be disposable.
/// </summary>
public interface IReaction : IDisposable
{
    LeafDomain Target { get; }
}
