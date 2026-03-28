using System.Collections.Generic;
using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.Navigation2D.Systems;

/// <summary>
/// Ear-clipping triangulation for simple polygons.
/// Converts a polygon (list of vertices) into triangles for the nav mesh.
/// </summary>
internal static class Triangulator
{
    /// <summary>
    /// Triangulate a simple polygon using ear-clipping.
    /// Vertices should be wound counter-clockwise.
    /// Returns list of (v0, v1, v2) index triples into the input array.
    /// </summary>
    public static List<(int A, int B, int C)> Triangulate(Vector2[] vertices, int count)
    {
        var triangles = new List<(int, int, int)>(count - 2);
        if (count < 3) return triangles;

        // Create index list
        var indices = new List<int>(count);

        // Ensure CCW winding
        if (GetSignedArea(vertices, count) < (Float)0)
        {
            for (int i = count - 1; i >= 0; i--)
                indices.Add(i);
        }
        else
        {
            for (int i = 0; i < count; i++)
                indices.Add(i);
        }

        int n = indices.Count;
        int attempts = 0;
        int maxAttempts = n * n; // Safety limit

        while (n > 2 && attempts < maxAttempts)
        {
            bool earFound = false;

            for (int i = 0; i < n; i++)
            {
                int prev = (i + n - 1) % n;
                int next = (i + 1) % n;

                var a = vertices[indices[prev]];
                var b = vertices[indices[i]];
                var c = vertices[indices[next]];

                // Check if this is a convex vertex
                if (Cross(b - a, c - b) <= (Float)0)
                {
                    attempts++;
                    continue;
                }

                // Check if any other vertex is inside this triangle
                bool isEar = true;
                for (int j = 0; j < n; j++)
                {
                    if (j == prev || j == i || j == next) continue;
                    if (PointInTriangle(vertices[indices[j]], a, b, c))
                    {
                        isEar = false;
                        break;
                    }
                }

                if (isEar)
                {
                    triangles.Add((indices[prev], indices[i], indices[next]));
                    indices.RemoveAt(i);
                    n--;
                    earFound = true;
                    break;
                }

                attempts++;
            }

            if (!earFound)
            {
                attempts++;
            }
        }

        return triangles;
    }

    private static Float GetSignedArea(Vector2[] vertices, int count)
    {
        Float area = (Float)0;
        for (int i = 0; i < count; i++)
        {
            int j = (i + 1) % count;
            area += vertices[i].X * vertices[j].Y;
            area -= vertices[j].X * vertices[i].Y;
        }
        return area / (Float)2;
    }

    private static Float Cross(Vector2 a, Vector2 b)
    {
        return a.X * b.Y - a.Y * b.X;
    }

    private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        var d1 = Sign(p, a, b);
        var d2 = Sign(p, b, c);
        var d3 = Sign(p, c, a);

        bool hasNeg = (d1 < (Float)0) || (d2 < (Float)0) || (d3 < (Float)0);
        bool hasPos = (d1 > (Float)0) || (d2 > (Float)0) || (d3 > (Float)0);

        return !(hasNeg && hasPos);
    }

    private static Float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
    {
        return (p1.X - p3.X) * (p2.Y - p3.Y) - (p2.X - p3.X) * (p1.Y - p3.Y);
    }
}
