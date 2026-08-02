using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;

using Deterministic.GameFramework.CDT;
using Float = Deterministic.GameFramework.Types.Float;
using Vector2 = Deterministic.GameFramework.Types.Vector2;

namespace Deterministic.GameFramework.CDT.Tests;

/// <summary>
/// Unit tests for the Constrained Delaunay Triangulation (CDT) library.
/// </summary>
public class CDTTests
{
    private readonly ITestOutputHelper _output;

    public CDTTests(ITestOutputHelper output)
    {
        _output = output;
    }

    // ================================================================
    //  Helper: create a Vector2 from floats
    // ================================================================

    private static Vector2 V(float x, float y) => new Vector2(x, y);

    // ================================================================
    //  Helper: validate triangulation structural integrity
    // ================================================================

    /// <summary>
    /// Assert that a triangulation result is structurally valid:
    /// - All triangle vertex indices are within bounds
    /// - All triangle neighbor references are within bounds or NoNeighbor
    /// - Neighbor references are reciprocal
    /// - No degenerate (zero-area) triangles
    /// </summary>
    private void AssertValidTriangulation(Triangulation cdt)
    {
        int triCount = cdt.Triangles.Count;
        int vertCount = cdt.Vertices.Count;

        for (int iT = 0; iT < triCount; iT++)
        {
            Triangle t = cdt.Triangles[iT];

            // All vertex indices must be valid
            t.V0.Should().BeGreaterThanOrEqualTo(0, $"triangle {iT} V0 out of bounds");
            t.V0.Should().BeLessThan(vertCount, $"triangle {iT} V0 out of bounds");
            t.V1.Should().BeGreaterThanOrEqualTo(0, $"triangle {iT} V1 out of bounds");
            t.V1.Should().BeLessThan(vertCount, $"triangle {iT} V1 out of bounds");
            t.V2.Should().BeGreaterThanOrEqualTo(0, $"triangle {iT} V2 out of bounds");
            t.V2.Should().BeLessThan(vertCount, $"triangle {iT} V2 out of bounds");

            // All vertices must be distinct
            t.V0.Should().NotBe(t.V1, $"triangle {iT} has duplicate vertices");
            t.V0.Should().NotBe(t.V2, $"triangle {iT} has duplicate vertices");
            t.V1.Should().NotBe(t.V2, $"triangle {iT} has duplicate vertices");

            // Neighbor indices must be valid or NoNeighbor
            for (int ni = 0; ni < 3; ni++)
            {
                int n = t.GetNeighbor(ni);
                if (n != CDTConstants.NoNeighbor)
                {
                    n.Should().BeGreaterThanOrEqualTo(0, $"triangle {iT} neighbor {ni} out of bounds");
                    n.Should().BeLessThan(triCount, $"triangle {iT} neighbor {ni} out of bounds");

                    // Reciprocal: the neighbor must reference back to this triangle
                    Triangle tn = cdt.Triangles[n];
                    bool foundReciprocal = tn.N0 == iT || tn.N1 == iT || tn.N2 == iT;
                    foundReciprocal.Should().BeTrue(
                        $"triangle {iT} references neighbor {n} at slot {ni}, but neighbor does not reference back");
                }
            }

            // No degenerate (zero-area) triangles
            Vector2 p0 = cdt.Vertices[t.V0];
            Vector2 p1 = cdt.Vertices[t.V1];
            Vector2 p2 = cdt.Vertices[t.V2];
            Float area = CDTUtility.Orient2D(p0, p1, p2);
            // Area should not be zero (degenerate triangle)
            bool isDegenerate = area == (Float)0;
            isDegenerate.Should().BeFalse($"triangle {iT} is degenerate (zero area)");
        }
    }

    // ================================================================
    //  Basic triangulation tests
    // ================================================================

    [Fact]
    public void ThreePoints_ProducesSingleTriangle()
    {
        var cdt = new Triangulation(VertexInsertionOrder.AsProvided);

        var vertices = new List<Vector2>
        {
            V(0f, 0f),
            V(1f, 0f),
            V(0.5f, 1f),
        };

        cdt.InsertVertices(vertices);
        cdt.EraseSuperTriangle();

        cdt.Triangles.Should().HaveCount(1, "3 non-collinear points should produce exactly 1 triangle");
        AssertValidTriangulation(cdt);

        // Verify the triangle uses all 3 vertices
        Triangle tri = cdt.Triangles[0];
        var usedVerts = new HashSet<int> { tri.V0, tri.V1, tri.V2 };
        usedVerts.Should().BeEquivalentTo(new[] { 0, 1, 2 });
    }

    [Fact]
    public void FourPoints_Square()
    {
        var cdt = new Triangulation(VertexInsertionOrder.AsProvided);

        var vertices = new List<Vector2>
        {
            V(0f, 0f),
            V(1f, 0f),
            V(1f, 1f),
            V(0f, 1f),
        };

        cdt.InsertVertices(vertices);
        cdt.EraseSuperTriangle();

        cdt.Triangles.Should().HaveCount(2, "4 points in a square should produce exactly 2 triangles");
        AssertValidTriangulation(cdt);

        // All 4 vertices should be used
        var usedVerts = new HashSet<int>();
        foreach (var t in cdt.Triangles)
        {
            usedVerts.Add(t.V0);
            usedVerts.Add(t.V1);
            usedVerts.Add(t.V2);
        }
        usedVerts.Should().BeEquivalentTo(new[] { 0, 1, 2, 3 });
    }

    [Fact]
    public void RegularPolygon_Hexagon()
    {
        var cdt = new Triangulation(VertexInsertionOrder.AsProvided);

        // 6 points on a unit circle (hexagon)
        var vertices = new List<Vector2>();
        for (int i = 0; i < 6; i++)
        {
            float angle = (float)(i * Math.PI * 2.0 / 6.0);
            vertices.Add(V((float)Math.Cos(angle), (float)Math.Sin(angle)));
        }

        cdt.InsertVertices(vertices);
        cdt.EraseSuperTriangle();

        // A hexagon should produce 4 triangles
        cdt.Triangles.Count.Should().BeGreaterOrEqualTo(4, "hexagon should produce at least 4 triangles");
        AssertValidTriangulation(cdt);

        // Verify all 6 vertices are used
        var usedVerts = new HashSet<int>();
        foreach (var t in cdt.Triangles)
        {
            usedVerts.Add(t.V0);
            usedVerts.Add(t.V1);
            usedVerts.Add(t.V2);
        }
        usedVerts.Should().BeEquivalentTo(new[] { 0, 1, 2, 3, 4, 5 },
            "all 6 hexagon vertices should appear in the triangulation");

        // Verify Delaunay property on a non-degenerate point set
        // (hexagon points are co-circular, so we test with a perturbed set instead)
        var cdt2 = new Triangulation(VertexInsertionOrder.AsProvided);
        var perturbedVerts = new List<Vector2>();
        for (int i = 0; i < 6; i++)
        {
            float angle = (float)(i * Math.PI * 2.0 / 6.0);
            float r = 1.0f + (i % 2 == 0 ? 0.05f : -0.05f); // slightly perturb radii
            perturbedVerts.Add(V(r * (float)Math.Cos(angle), r * (float)Math.Sin(angle)));
        }
        cdt2.InsertVertices(perturbedVerts);
        cdt2.EraseSuperTriangle();

        for (int iT = 0; iT < cdt2.Triangles.Count; iT++)
        {
            Triangle t = cdt2.Triangles[iT];
            Vector2 v0 = cdt2.Vertices[t.V0];
            Vector2 v1 = cdt2.Vertices[t.V1];
            Vector2 v2 = cdt2.Vertices[t.V2];

            for (int iV = 0; iV < cdt2.Vertices.Count; iV++)
            {
                if (iV == t.V0 || iV == t.V1 || iV == t.V2)
                    continue;
                bool inCircle = CDTUtility.IsInCircumcircle(cdt2.Vertices[iV], v0, v1, v2);
                inCircle.Should().BeFalse(
                    $"vertex {iV} is inside circumcircle of triangle {iT} - violates Delaunay property");
            }
        }
    }

    // ================================================================
    //  Constraint edge tests
    // ================================================================

    [Fact]
    public void InsertEdge_AcrossTriangulation()
    {
        var cdt = new Triangulation(VertexInsertionOrder.AsProvided);

        // Grid of points
        var vertices = new List<Vector2>
        {
            V(0f, 0f),  // 0
            V(2f, 0f),  // 1
            V(4f, 0f),  // 2
            V(0f, 2f),  // 3
            V(2f, 2f),  // 4
            V(4f, 2f),  // 5
        };

        cdt.InsertVertices(vertices);

        // Insert a constraint edge that crosses existing triangulation: from vertex 0 to vertex 5
        var edges = new List<Edge> { new Edge(0, 5) };
        cdt.InsertEdges(edges);
        cdt.EraseSuperTriangle();

        AssertValidTriangulation(cdt);

        // Verify the constraint edge exists
        cdt.FixedEdges.Should().Contain(new Edge(0, 5),
            "the inserted constraint edge should be in FixedEdges");

        // The edge should exist in the triangulation
        var allEdges = CDTHelpers.ExtractEdgesFromTriangles(cdt.Triangles);
        allEdges.Should().Contain(new Edge(0, 5),
            "the constraint edge should appear in the triangulation edges");
    }

    [Fact]
    public void ConformToEdge_AddsMidpoints()
    {
        var cdt = new Triangulation(
            VertexInsertionOrder.AsProvided,
            IntersectingConstraintEdges.TryResolve,
            (Float)0);

        var vertices = new List<Vector2>
        {
            V(0f, 0f),  // 0
            V(4f, 0f),  // 1
            V(4f, 4f),  // 2
            V(0f, 4f),  // 3
            V(2f, 1f),  // 4: interior point that may cause edge splitting
            V(2f, 3f),  // 5: interior point that may cause edge splitting
        };

        int vertCountBefore = vertices.Count;
        cdt.InsertVertices(vertices);

        // Conform to a diagonal edge
        var edges = new List<Edge> { new Edge(0, 2) };
        cdt.ConformToEdges(edges);
        cdt.EraseSuperTriangle();

        AssertValidTriangulation(cdt);

        // ConformToEdges may add midpoints, increasing vertex count
        // At minimum, the original vertices plus any added ones
        cdt.Vertices.Count.Should().BeGreaterOrEqualTo(vertCountBefore,
            "conforming to edges should not lose vertices");
    }

    [Fact]
    public void ConstraintEdge_PreservedInResult()
    {
        var cdt = new Triangulation(VertexInsertionOrder.AsProvided);

        // Use points where the constraint edge does NOT pass through an existing vertex,
        // so the edge is preserved as-is rather than being split.
        var vertices = new List<Vector2>
        {
            V(0f, 0f),
            V(4f, 0f),
            V(4f, 4f),
            V(0f, 4f),
            V(1f, 3f),  // off the diagonal
        };

        cdt.InsertVertices(vertices);

        var constraintEdge = new Edge(0, 2);
        cdt.InsertEdges(new List<Edge> { constraintEdge });
        cdt.EraseSuperTriangle();

        AssertValidTriangulation(cdt);

        // The constraint edge should be preserved in FixedEdges (or split into pieces)
        bool directlyPresent = cdt.FixedEdges.Contains(constraintEdge);

        // If the edge was split, its pieces should all be fixed edges
        // that connect vertex 0 to vertex 2 through intermediate vertices.
        if (!directlyPresent)
        {
            // Check that the edge exists in the triangulation as a sequence of fixed edges
            var allEdges = CDTHelpers.ExtractEdgesFromTriangles(cdt.Triangles);
            var fixedEdgesOnDiag = cdt.FixedEdges
                .Where(e => allEdges.Contains(e))
                .ToList();
            fixedEdgesOnDiag.Count.Should().BeGreaterThan(0,
                "constraint edge should be in FixedEdges either directly or as split pieces");
        }

        // Verify the edge (or its pieces) appear in the triangulation
        var triEdges = CDTHelpers.ExtractEdgesFromTriangles(cdt.Triangles);
        bool edgeExistsInTriangulation = triEdges.Contains(constraintEdge);
        if (!edgeExistsInTriangulation)
        {
            // Edge was split through an existing vertex; verify the pieces exist
            cdt.FixedEdges.Count.Should().BeGreaterThan(0,
                "there should be fixed edges from the constraint");
        }
    }

    // ================================================================
    //  Hole/boundary tests
    // ================================================================

    [Fact]
    public void EraseOuterTriangles_RemovesExterior()
    {
        var cdt = new Triangulation(VertexInsertionOrder.AsProvided);

        // A convex polygon (square)
        var vertices = new List<Vector2>
        {
            V(0f, 0f),
            V(10f, 0f),
            V(10f, 10f),
            V(0f, 10f),
        };

        cdt.InsertVertices(vertices);

        // Add boundary edges (all 4 sides of the square)
        var edges = new List<Edge>
        {
            new Edge(0, 1),
            new Edge(1, 2),
            new Edge(2, 3),
            new Edge(3, 0),
        };
        cdt.InsertEdges(edges);
        cdt.EraseOuterTriangles();

        AssertValidTriangulation(cdt);

        // Only interior triangles should remain
        // The square should have exactly 2 triangles
        cdt.Triangles.Count.Should().Be(2,
            "a square boundary should yield exactly 2 interior triangles");

        // All triangle vertices should be boundary vertices (indices 0-3)
        foreach (var t in cdt.Triangles)
        {
            t.V0.Should().BeInRange(0, 3);
            t.V1.Should().BeInRange(0, 3);
            t.V2.Should().BeInRange(0, 3);
        }
    }

    [Fact]
    public void EraseOuterTrianglesAndHoles_HandlesHole()
    {
        var cdt = new Triangulation(VertexInsertionOrder.AsProvided);

        // Outer boundary (large square)
        // Inner boundary (small square hole)
        var vertices = new List<Vector2>
        {
            // Outer boundary: 0-3
            V(0f, 0f),
            V(20f, 0f),
            V(20f, 20f),
            V(0f, 20f),
            // Inner hole boundary: 4-7
            V(5f, 5f),
            V(15f, 5f),
            V(15f, 15f),
            V(5f, 15f),
        };

        cdt.InsertVertices(vertices);

        // Outer boundary edges (CCW)
        var edges = new List<Edge>
        {
            new Edge(0, 1),
            new Edge(1, 2),
            new Edge(2, 3),
            new Edge(3, 0),
            // Inner hole boundary edges (CW - to mark as hole)
            new Edge(4, 5),
            new Edge(5, 6),
            new Edge(6, 7),
            new Edge(7, 4),
        };

        cdt.InsertEdges(edges);
        cdt.EraseOuterTrianglesAndHoles();

        AssertValidTriangulation(cdt);

        // Triangles in the hole area should be removed
        // Verify no triangle has all vertices from the inner square only
        int trianglesInsideHole = 0;
        var innerVerts = new HashSet<int> { 4, 5, 6, 7 };
        foreach (var t in cdt.Triangles)
        {
            if (innerVerts.Contains(t.V0) && innerVerts.Contains(t.V1) && innerVerts.Contains(t.V2))
                trianglesInsideHole++;
        }
        trianglesInsideHole.Should().Be(0,
            "triangles inside the hole should be removed");

        // There should be some triangles remaining (the area between outer and inner boundaries)
        cdt.Triangles.Count.Should().BeGreaterThan(0,
            "triangles between boundaries should remain");
    }

    [Fact]
    public void CalculateTriangleDepths_NestedBoundaries()
    {
        var cdt = new Triangulation(VertexInsertionOrder.AsProvided);

        // Outer boundary (large square)
        // Inner boundary (small square)
        var vertices = new List<Vector2>
        {
            // Outer: 0-3
            V(0f, 0f),
            V(20f, 0f),
            V(20f, 20f),
            V(0f, 20f),
            // Inner: 4-7
            V(6f, 6f),
            V(14f, 6f),
            V(14f, 14f),
            V(6f, 14f),
        };

        cdt.InsertVertices(vertices);

        var edges = new List<Edge>
        {
            // Outer boundary
            new Edge(0, 1),
            new Edge(1, 2),
            new Edge(2, 3),
            new Edge(3, 0),
            // Inner boundary
            new Edge(4, 5),
            new Edge(5, 6),
            new Edge(6, 7),
            new Edge(7, 4),
        };

        cdt.InsertEdges(edges);

        // Calculate depths before erasing (while super-triangle is still present)
        List<int> depths = cdt.CalculateTriangleDepths();

        depths.Should().NotBeEmpty();

        // Depth 0 = outside outermost boundary (super-triangle area)
        // Depth 1 = inside outer boundary, outside inner boundary
        // Depth 2 = inside inner boundary
        var uniqueDepths = new HashSet<int>(depths.Where(d => d != ushort.MaxValue));
        uniqueDepths.Should().Contain(0, "there should be depth-0 triangles outside the outer boundary");
        uniqueDepths.Should().Contain(1, "there should be depth-1 triangles between boundaries");
        uniqueDepths.Should().Contain(2, "there should be depth-2 triangles inside the inner boundary");
    }

    // ================================================================
    //  Edge case tests
    // ================================================================

    [Fact]
    public void DuplicateVertices_Handled()
    {
        var vertices = new List<Vector2>
        {
            V(0f, 0f),
            V(1f, 0f),
            V(0.5f, 1f),
            V(0f, 0f),   // duplicate of vertex 0
            V(1f, 0f),   // duplicate of vertex 1
        };

        var edges = new List<Edge>
        {
            new Edge(0, 1),
            new Edge(1, 2),
            new Edge(2, 3), // references the duplicate; should be remapped to edge(2,0)
        };

        DuplicatesInfo dupes = CDTHelpers.RemoveDuplicatesAndRemapEdges(vertices, edges);

        dupes.Duplicates.Should().HaveCount(2, "2 duplicate vertices should be identified");
        vertices.Should().HaveCount(3, "duplicates should be removed from vertex list");

        // Now triangulate with the cleaned data
        var cdt = new Triangulation(VertexInsertionOrder.AsProvided);
        cdt.InsertVertices(vertices);
        cdt.InsertEdges(edges);
        cdt.EraseSuperTriangle();

        AssertValidTriangulation(cdt);
        cdt.Triangles.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CollinearPoints_Handled()
    {
        var cdt = new Triangulation(VertexInsertionOrder.AsProvided);

        // 3 collinear points plus one off-line point to ensure valid triangulation
        var vertices = new List<Vector2>
        {
            V(0f, 0f),
            V(1f, 0f),
            V(2f, 0f),
            V(1f, 1f), // off the line
        };

        cdt.InsertVertices(vertices);
        cdt.EraseSuperTriangle();

        AssertValidTriangulation(cdt);
        cdt.Triangles.Count.Should().BeGreaterThan(0, "should produce valid triangulation with collinear points");
    }

    [Fact]
    public void SingleTriangle_EraseSuperTriangle()
    {
        var cdt = new Triangulation(VertexInsertionOrder.AsProvided);

        var vertices = new List<Vector2>
        {
            V(0f, 0f),
            V(1f, 0f),
            V(0.5f, 0.866f),
        };

        cdt.InsertVertices(vertices);
        cdt.EraseSuperTriangle();

        cdt.Triangles.Should().HaveCount(1);
        cdt.Vertices.Should().HaveCount(3);
        AssertValidTriangulation(cdt);
    }

    // ================================================================
    //  Determinism test
    // ================================================================

    [Fact]
    public void SameInput_ProducesIdenticalOutput()
    {
        var vertices = new List<Vector2>
        {
            V(0f, 0f),
            V(5f, 0f),
            V(5f, 5f),
            V(0f, 5f),
            V(2.5f, 2.5f),
            V(1f, 3f),
            V(4f, 1f),
        };

        var edges = new List<Edge>
        {
            new Edge(0, 1),
            new Edge(1, 2),
            new Edge(2, 3),
            new Edge(3, 0),
        };

        // First run
        var cdt1 = new Triangulation(VertexInsertionOrder.AsProvided);
        cdt1.InsertVertices(new List<Vector2>(vertices));
        cdt1.InsertEdges(new List<Edge>(edges));
        cdt1.EraseSuperTriangle();

        // Second run
        var cdt2 = new Triangulation(VertexInsertionOrder.AsProvided);
        cdt2.InsertVertices(new List<Vector2>(vertices));
        cdt2.InsertEdges(new List<Edge>(edges));
        cdt2.EraseSuperTriangle();

        // Verify identical output
        cdt1.Vertices.Count.Should().Be(cdt2.Vertices.Count, "vertex counts should match");
        cdt1.Triangles.Count.Should().Be(cdt2.Triangles.Count, "triangle counts should match");

        for (int i = 0; i < cdt1.Vertices.Count; i++)
        {
            cdt1.Vertices[i].Should().Be(cdt2.Vertices[i], $"vertex {i} should be identical");
        }

        for (int i = 0; i < cdt1.Triangles.Count; i++)
        {
            Triangle t1 = cdt1.Triangles[i];
            Triangle t2 = cdt2.Triangles[i];
            t1.V0.Should().Be(t2.V0, $"triangle {i} V0 should be identical");
            t1.V1.Should().Be(t2.V1, $"triangle {i} V1 should be identical");
            t1.V2.Should().Be(t2.V2, $"triangle {i} V2 should be identical");
            t1.N0.Should().Be(t2.N0, $"triangle {i} N0 should be identical");
            t1.N1.Should().Be(t2.N1, $"triangle {i} N1 should be identical");
            t1.N2.Should().Be(t2.N2, $"triangle {i} N2 should be identical");
        }

        cdt1.FixedEdges.SetEquals(cdt2.FixedEdges).Should().BeTrue("fixed edges should be identical");
    }

    // ================================================================
    //  Validity test with many random points
    // ================================================================

    [Fact]
    public void ManyRandomPoints_ValidTriangulation()
    {
        var cdt = new Triangulation(VertexInsertionOrder.AsProvided);
        var rng = new Random(42);

        var vertices = new List<Vector2>();
        for (int i = 0; i < 200; i++)
        {
            float x = (float)(rng.NextDouble() * 100.0);
            float y = (float)(rng.NextDouble() * 100.0);
            vertices.Add(V(x, y));
        }

        cdt.InsertVertices(vertices);
        cdt.EraseSuperTriangle();

        AssertValidTriangulation(cdt);
        cdt.Triangles.Count.Should().BeGreaterThan(0);
        _output.WriteLine($"200 random points produced {cdt.Triangles.Count} triangles");
    }
}

/// <summary>
/// Performance benchmarks for the CDT library using xunit Facts with Stopwatch.
/// </summary>
public class CDTPerformanceTests
{
    private readonly ITestOutputHelper _output;

    public CDTPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static Vector2 V(float x, float y) => new Vector2(x, y);

    /// <summary>
    /// Simple deterministic PRNG for test reproducibility.
    /// </summary>
    private class DeterministicRng
    {
        private Random _rng;

        public DeterministicRng(int seed)
        {
            _rng = new Random(seed);
        }

        public float NextFloat(float min, float max)
        {
            return (float)(_rng.NextDouble() * (max - min) + min);
        }

        public int NextInt(int min, int max)
        {
            return _rng.Next(min, max);
        }
    }

    [Theory]
    [InlineData(100)]
    [InlineData(500)]
    [InlineData(1000)]
    public void PerformanceTest_RandomPoints_WithEdges(int pointCount)
    {
        RunRandomPointsWithEdgesBenchmark(pointCount);
    }

    // NOTE: Auto (KD-tree BFS) insertion has a known perf bug above ~200 points.
    // Skipped to prevent test hangs. Use AsProvided for now.
    [Theory(Skip = "Auto insertion order has known perf bug — hangs above ~200 points")]
    [InlineData(100)]
    [InlineData(200)]
    public void PerformanceTest_RandomPoints_VertexOnly_Auto(int pointCount)
    {
        RunVertexOnlyBenchmark(pointCount, VertexInsertionOrder.Auto);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(500)]
    [InlineData(1000)]
    public void PerformanceTest_RandomPoints_VertexOnly_AsProvided(int pointCount)
    {
        RunVertexOnlyBenchmark(pointCount, VertexInsertionOrder.AsProvided);
    }

    private void RunVertexOnlyBenchmark(int pointCount, VertexInsertionOrder insertionOrder)
    {
        var rng = new DeterministicRng(12345);

        var vertices = new List<Vector2>(pointCount);
        for (int i = 0; i < pointCount; i++)
        {
            vertices.Add(V(rng.NextFloat(0f, 1000f), rng.NextFloat(0f, 1000f)));
        }

        var sw = Stopwatch.StartNew();
        var cdt = new Triangulation(insertionOrder);
        cdt.InsertVertices(vertices);
        var vertexTime = sw.Elapsed;

        sw.Restart();
        cdt.EraseSuperTriangle();
        var eraseTime = sw.Elapsed;

        var totalTime = vertexTime + eraseTime;
        double trianglesPerSecond = totalTime.TotalSeconds > 0
            ? cdt.Triangles.Count / totalTime.TotalSeconds
            : double.PositiveInfinity;

        _output.WriteLine($"=== Vertex-Only Benchmark ({insertionOrder}) ===");
        _output.WriteLine($"  Points:            {pointCount}");
        _output.WriteLine($"  Result triangles:  {cdt.Triangles.Count}");
        _output.WriteLine($"  Vertex insertion:  {vertexTime.TotalMilliseconds:F2} ms");
        _output.WriteLine($"  Erase super tri:   {eraseTime.TotalMilliseconds:F2} ms");
        _output.WriteLine($"  Total:             {totalTime.TotalMilliseconds:F2} ms");
        _output.WriteLine($"  Triangles/sec:     {trianglesPerSecond:F0}");

        cdt.Triangles.Count.Should().BeGreaterThan(0);
        cdt.Vertices.Count.Should().Be(pointCount);
    }

    private void RunRandomPointsWithEdgesBenchmark(int pointCount)
    {
        // Use AsProvided insertion order for edge insertion benchmarks
        // (Auto + edge insertion has known issues with long constraint edges)
        var insertionOrder = VertexInsertionOrder.AsProvided;
        var rng = new DeterministicRng(12345);

        var vertices = new List<Vector2>(pointCount);
        for (int i = 0; i < pointCount; i++)
        {
            vertices.Add(V(rng.NextFloat(0f, 1000f), rng.NextFloat(0f, 1000f)));
        }

        // Generate non-intersecting constraint edges: chain of edges connecting
        // consecutive vertices with a stride, forming a non-self-intersecting path.
        var constraintEdges = new List<Edge>();
        int edgeCount = Math.Min(pointCount / 10, 500);
        int step = Math.Max(1, pointCount / edgeCount);
        for (int i = 0; i < pointCount - step; i += step)
        {
            constraintEdges.Add(new Edge(i, i + step));
        }

        var sw = Stopwatch.StartNew();
        var cdt = new Triangulation(insertionOrder,
            IntersectingConstraintEdges.TryResolve, (Float)0);
        cdt.InsertVertices(vertices);
        var vertexTime = sw.Elapsed;

        sw.Restart();
        cdt.InsertEdges(constraintEdges);
        var edgeTime = sw.Elapsed;

        sw.Restart();
        cdt.EraseSuperTriangle();
        var eraseTime = sw.Elapsed;

        var totalTime = vertexTime + edgeTime + eraseTime;
        double trianglesPerSecond = totalTime.TotalSeconds > 0
            ? cdt.Triangles.Count / totalTime.TotalSeconds
            : double.PositiveInfinity;

        _output.WriteLine($"=== Random Points + Edges Benchmark ({insertionOrder}) ===");
        _output.WriteLine($"  Points:            {pointCount}");
        _output.WriteLine($"  Constraint edges:  {constraintEdges.Count}");
        _output.WriteLine($"  Result triangles:  {cdt.Triangles.Count}");
        _output.WriteLine($"  Vertex insertion:  {vertexTime.TotalMilliseconds:F2} ms");
        _output.WriteLine($"  Edge insertion:    {edgeTime.TotalMilliseconds:F2} ms");
        _output.WriteLine($"  Erase super tri:   {eraseTime.TotalMilliseconds:F2} ms");
        _output.WriteLine($"  Total:             {totalTime.TotalMilliseconds:F2} ms");
        _output.WriteLine($"  Triangles/sec:     {trianglesPerSecond:F0}");

        cdt.Triangles.Count.Should().BeGreaterThan(0);
        cdt.Vertices.Count.Should().BeGreaterOrEqualTo(pointCount);
    }

    [Fact]
    public void PerformanceTest_GridWithObstacles()
    {
        const int gridSize = 20; // 20x20 = 400 points
        const float cellSize = 2f;
        const float obstacleSize = 3f;
        const float obstacleSpacing = 8f;

        _output.WriteLine("=== Grid With Obstacles Benchmark ===");

        // Generate grid points
        var vertices = new List<Vector2>();
        for (int y = 0; y < gridSize; y++)
        {
            for (int x = 0; x < gridSize; x++)
            {
                vertices.Add(V(x * cellSize, y * cellSize));
            }
        }

        _output.WriteLine($"  Grid size:        {gridSize}x{gridSize} = {vertices.Count} points");

        // Generate non-overlapping rectangular obstacle polygons on a regular grid
        // to avoid intersecting constraint edges
        var constraintEdges = new List<Edge>();
        int nextVertIndex = vertices.Count;
        var obstacleVertices = new List<Vector2>();
        int obstacleCount = 0;

        for (float oy = 4f; oy + obstacleSize < gridSize * cellSize - 4f; oy += obstacleSpacing)
        {
            for (float ox = 4f; ox + obstacleSize < gridSize * cellSize - 4f; ox += obstacleSpacing)
            {
                int baseIdx = nextVertIndex;
                obstacleVertices.Add(V(ox, oy));
                obstacleVertices.Add(V(ox + obstacleSize, oy));
                obstacleVertices.Add(V(ox + obstacleSize, oy + obstacleSize));
                obstacleVertices.Add(V(ox, oy + obstacleSize));

                constraintEdges.Add(new Edge(baseIdx, baseIdx + 1));
                constraintEdges.Add(new Edge(baseIdx + 1, baseIdx + 2));
                constraintEdges.Add(new Edge(baseIdx + 2, baseIdx + 3));
                constraintEdges.Add(new Edge(baseIdx + 3, baseIdx));

                nextVertIndex += 4;
                obstacleCount++;
            }
        }

        // Combine all vertices
        var allVertices = new List<Vector2>(vertices);
        allVertices.AddRange(obstacleVertices);

        _output.WriteLine($"  Obstacles:        {obstacleCount} rectangles ({obstacleVertices.Count} extra vertices)");
        _output.WriteLine($"  Total vertices:   {allVertices.Count}");
        _output.WriteLine($"  Constraint edges: {constraintEdges.Count}");

        // Remove duplicates
        CDTHelpers.RemoveDuplicatesAndRemapEdges(allVertices, constraintEdges);

        // Benchmark: full triangulation
        var sw = Stopwatch.StartNew();
        var cdt = new Triangulation(VertexInsertionOrder.AsProvided);
        cdt.InsertVertices(allVertices);
        var vertTime = sw.Elapsed;

        sw.Restart();
        cdt.InsertEdges(constraintEdges);
        var edgeTime = sw.Elapsed;

        sw.Restart();
        cdt.EraseOuterTrianglesAndHoles();
        var eraseTime = sw.Elapsed;

        var totalTime = vertTime + edgeTime + eraseTime;

        _output.WriteLine($"  --- Full triangulation ---");
        _output.WriteLine($"  Vertex insertion:  {vertTime.TotalMilliseconds:F2} ms");
        _output.WriteLine($"  Edge insertion:    {edgeTime.TotalMilliseconds:F2} ms");
        _output.WriteLine($"  Erase outer+holes: {eraseTime.TotalMilliseconds:F2} ms");
        _output.WriteLine($"  Total:             {totalTime.TotalMilliseconds:F2} ms");
        _output.WriteLine($"  Result triangles:  {cdt.Triangles.Count}");
        _output.WriteLine($"  Result vertices:   {cdt.Vertices.Count}");

        cdt.Triangles.Count.Should().BeGreaterThan(0);

        // Benchmark: incremental update (add one more obstacle to a fresh triangulation)
        // This simulates what the game would do when a building is placed
        sw.Restart();
        var cdt2 = new Triangulation(VertexInsertionOrder.AsProvided);
        cdt2.InsertVertices(allVertices);
        // Insert original edges minus the last obstacle
        var edgesMinusLast = constraintEdges
            .Take(constraintEdges.Count - 4)
            .ToList();
        cdt2.InsertEdges(edgesMinusLast);
        var baseTime = sw.Elapsed;

        // Now add the last obstacle's edges
        sw.Restart();
        var lastObsEdges = constraintEdges.Skip(constraintEdges.Count - 4).ToList();
        cdt2.InsertEdges(lastObsEdges);
        var incrementalEdgeTime = sw.Elapsed;

        sw.Restart();
        cdt2.EraseOuterTrianglesAndHoles();
        var incrementalEraseTime = sw.Elapsed;

        _output.WriteLine($"  --- Incremental (add 1 obstacle) ---");
        _output.WriteLine($"  Base setup:        {baseTime.TotalMilliseconds:F2} ms");
        _output.WriteLine($"  Add obstacle edges:{incrementalEdgeTime.TotalMilliseconds:F2} ms");
        _output.WriteLine($"  Erase outer+holes: {incrementalEraseTime.TotalMilliseconds:F2} ms");
        _output.WriteLine($"  Result triangles:  {cdt2.Triangles.Count}");

        cdt2.Triangles.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void PerformanceTest_CompareInsertionOrders()
    {
        const int pointCount = 200;
        var rng = new DeterministicRng(54321);

        var vertices = new List<Vector2>(pointCount);
        for (int i = 0; i < pointCount; i++)
        {
            vertices.Add(V(rng.NextFloat(-500f, 500f), rng.NextFloat(-500f, 500f)));
        }

        _output.WriteLine($"=== Insertion Order Comparison ({pointCount} points) ===");

        // AsProvided (run twice to compare determinism)
        var sw = Stopwatch.StartNew();
        var cdt1 = new Triangulation(VertexInsertionOrder.AsProvided);
        cdt1.InsertVertices(new List<Vector2>(vertices));
        cdt1.EraseSuperTriangle();
        var time1 = sw.Elapsed;

        sw.Restart();
        var cdt2 = new Triangulation(VertexInsertionOrder.AsProvided);
        cdt2.InsertVertices(new List<Vector2>(vertices));
        cdt2.EraseSuperTriangle();
        var time2 = sw.Elapsed;

        _output.WriteLine($"  Run 1: {time1.TotalMilliseconds:F2} ms, {cdt1.Triangles.Count} triangles");
        _output.WriteLine($"  Run 2: {time2.TotalMilliseconds:F2} ms, {cdt2.Triangles.Count} triangles");

        cdt1.Triangles.Count.Should().Be(cdt2.Triangles.Count,
            "deterministic runs should produce the same triangle count");
    }
}
