using Deterministic.GameFramework.Navigation2D.Systems;
using Deterministic.GameFramework.Types;
using FluentAssertions;
using Xunit;

namespace Deterministic.GameFramework.Navigation.Tests;

public class TriangulatorTests
{
    [Fact]
    public void Triangle_ReturnsOneTriangle()
    {
        var verts = new Vector2[]
        {
            new(0, 0), new(10, 0), new(5, 10)
        };

        var result = Triangulator.Triangulate(verts, 3);

        result.Should().HaveCount(1);
    }

    [Fact]
    public void Square_ReturnsTwoTriangles()
    {
        var verts = new Vector2[]
        {
            new(0, 0), new(10, 0), new(10, 10), new(0, 10)
        };

        var result = Triangulator.Triangulate(verts, 4);

        result.Should().HaveCount(2);
    }

    [Fact]
    public void Pentagon_ReturnsThreeTriangles()
    {
        var verts = new Vector2[]
        {
            new(5, 0), new(10, 4), new(8, 10), new(2, 10), new(0, 4)
        };

        var result = Triangulator.Triangulate(verts, 5);

        result.Should().HaveCount(3);
    }

    [Fact]
    public void LessThanThreeVertices_ReturnsEmpty()
    {
        var verts = new Vector2[] { new(0, 0), new(1, 1) };
        var result = Triangulator.Triangulate(verts, 2);
        result.Should().BeEmpty();
    }

    [Fact]
    public void CW_Winding_StillTriangulates()
    {
        // Clockwise wound square
        var verts = new Vector2[]
        {
            new(0, 10), new(10, 10), new(10, 0), new(0, 0)
        };

        var result = Triangulator.Triangulate(verts, 4);

        result.Should().HaveCount(2);
    }

    [Fact]
    public void TriangleIndices_AreWithinBounds()
    {
        var verts = new Vector2[]
        {
            new(0, 0), new(10, 0), new(10, 10), new(0, 10)
        };

        var result = Triangulator.Triangulate(verts, 4);

        foreach (var (a, b, c) in result)
        {
            a.Should().BeInRange(0, 3);
            b.Should().BeInRange(0, 3);
            c.Should().BeInRange(0, 3);
        }
    }
}
