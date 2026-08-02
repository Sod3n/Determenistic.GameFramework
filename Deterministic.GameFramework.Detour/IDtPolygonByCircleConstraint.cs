using System;
using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.Detour
{
    public interface IDtPolygonByCircleConstraint
    {
        bool Apply(Span<Float> polyVerts, Vector3 circleCenter, Float radius, Span<Float> constrainedVerts, out int constrainedVertCount);
    }
}
