using System;
using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.Detour
{
    public class DtNoOpDtPolygonByCircleConstraint : IDtPolygonByCircleConstraint
    {
        public static readonly DtNoOpDtPolygonByCircleConstraint Shared = new DtNoOpDtPolygonByCircleConstraint();

        private DtNoOpDtPolygonByCircleConstraint()
        {
        }

        public bool Apply(Span<Float> polyVerts, Vector3 circleCenter, Float radius, Span<Float> constrainedVerts, out int constrainedVertCount)
        {
            polyVerts.CopyTo(constrainedVerts);
            constrainedVertCount = polyVerts.Length;
            return true;
        }
    }
}
