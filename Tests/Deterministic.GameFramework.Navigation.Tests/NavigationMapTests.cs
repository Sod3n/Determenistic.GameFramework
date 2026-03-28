using Deterministic.GameFramework.Navigation2D.Systems;
using Deterministic.GameFramework.Types;
using FluentAssertions;
using Xunit;

namespace Deterministic.GameFramework.Navigation.Tests;

public class NavigationMapTests
{
    private NavigationMap CreateSquareMap()
    {
        // A simple square nav mesh: two triangles forming a 10x10 square
        var map = new NavigationMap();
        map.AddTriangle(new Vector2(0, 0), new Vector2(10, 0), new Vector2(10, 10), regionEntityId: 1);
        map.AddTriangle(new Vector2(0, 0), new Vector2(10, 10), new Vector2(0, 10), regionEntityId: 1);
        map.RegionCosts[1] = ((Float)1, (Float)0);
        map.BuildAdjacency((Float)1.0f);
        return map;
    }

    private NavigationMap CreateTwoRegionMap()
    {
        // Two adjacent squares: region 1 (0-10) and region 2 (10-20)
        var map = new NavigationMap();
        // Region 1
        map.AddTriangle(new Vector2(0, 0), new Vector2(10, 0), new Vector2(10, 10), regionEntityId: 1);
        map.AddTriangle(new Vector2(0, 0), new Vector2(10, 10), new Vector2(0, 10), regionEntityId: 1);
        // Region 2
        map.AddTriangle(new Vector2(10, 0), new Vector2(20, 0), new Vector2(20, 10), regionEntityId: 2);
        map.AddTriangle(new Vector2(10, 0), new Vector2(20, 10), new Vector2(10, 10), regionEntityId: 2);

        map.RegionCosts[1] = ((Float)1, (Float)0);
        map.RegionCosts[2] = ((Float)1, (Float)0);
        map.BuildAdjacency((Float)1.0f);
        return map;
    }

    [Fact]
    public void FindTriangle_PointInside_ReturnsValidIndex()
    {
        var map = CreateSquareMap();

        var triIdx = map.FindTriangle(new Vector2(5, 5));
        triIdx.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void FindTriangle_PointOutside_ReturnsNegative()
    {
        var map = CreateSquareMap();

        var triIdx = map.FindTriangle(new Vector2(50, 50));
        triIdx.Should().Be(-1);
    }

    [Fact]
    public void FindClosestTriangle_PointOutside_ReturnsClosestPoint()
    {
        var map = CreateSquareMap();

        var (triIdx, closest) = map.FindClosestTriangle(new Vector2(15, 5));
        triIdx.Should().BeGreaterThanOrEqualTo(0);
        // Closest point should be on the edge at x=10
        ((float)closest.X).Should().BeApproximately(10f, 0.1f);
    }

    [Fact]
    public void FindTrianglePath_SameTriangle_ReturnsSingleElement()
    {
        var map = CreateSquareMap();

        var startTri = map.FindTriangle(new Vector2(2, 2));
        var path = map.FindTrianglePath(startTri, startTri);

        path.Should().NotBeNull();
        path.Should().HaveCount(1);
    }

    [Fact]
    public void FindTrianglePath_AcrossTriangles_ReturnsPath()
    {
        var map = CreateSquareMap();

        var startTri = map.FindTriangle(new Vector2(2, 2));
        var endTri = map.FindTriangle(new Vector2(8, 8));

        // These might be in different triangles
        if (startTri != endTri)
        {
            var path = map.FindTrianglePath(startTri, endTri);
            path.Should().NotBeNull();
            path!.Count.Should().BeGreaterThanOrEqualTo(2);
            path[0].Should().Be(startTri);
            path[^1].Should().Be(endTri);
        }
    }

    [Fact]
    public void FindTrianglePath_AcrossRegions_Works()
    {
        var map = CreateTwoRegionMap();

        var startTri = map.FindTriangle(new Vector2(2, 5));
        var endTri = map.FindTriangle(new Vector2(18, 5));

        startTri.Should().BeGreaterThanOrEqualTo(0);
        endTri.Should().BeGreaterThanOrEqualTo(0);

        var path = map.FindTrianglePath(startTri, endTri);
        path.Should().NotBeNull();
        path!.Count.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void SmoothPath_SimpleCase_ReturnsWaypoints()
    {
        var map = CreateSquareMap();

        var start = new Vector2(1, 1);
        var end = new Vector2(9, 9);
        var startTri = map.FindTriangle(start);
        var endTri = map.FindTriangle(end);

        var triPath = map.FindTrianglePath(startTri, endTri);
        triPath.Should().NotBeNull();

        var waypoints = map.SmoothPath(triPath!, start, end);
        waypoints.Should().NotBeEmpty();
        waypoints[0].Should().Be(start);
        waypoints[^1].Should().Be(end);
    }

    [Fact]
    public void Adjacency_SharedEdge_IsDetected()
    {
        var map = CreateSquareMap();

        // The two triangles share the diagonal edge, so they should be neighbors
        map.Adjacency[0].Should().NotBeEmpty("triangle 0 should have at least one neighbor");
        map.Adjacency[1].Should().NotBeEmpty("triangle 1 should have at least one neighbor");
    }

    [Fact]
    public void Adjacency_TwoRegions_AreConnected()
    {
        var map = CreateTwoRegionMap();

        // Region 1 and Region 2 share the x=10 edge
        bool anyConnection = false;
        for (int i = 0; i < map.Adjacency.Count; i++)
        {
            foreach (var edge in map.Adjacency[i])
            {
                if (map.TriangleRegionId[i] != map.TriangleRegionId[edge.NeighborIndex])
                {
                    anyConnection = true;
                    break;
                }
            }
            if (anyConnection) break;
        }

        anyConnection.Should().BeTrue("regions sharing an edge should be connected in the adjacency graph");
    }

    [Fact]
    public void Clear_ResetsAllState()
    {
        var map = CreateSquareMap();

        map.Clear();

        map.Triangles.Should().BeEmpty();
        map.Vertices.Should().BeEmpty();
        map.Adjacency.Should().BeEmpty();
        map.IsDirty.Should().BeTrue();
    }
}
