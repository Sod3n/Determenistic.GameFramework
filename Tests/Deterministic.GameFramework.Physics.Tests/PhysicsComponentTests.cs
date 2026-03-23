using Deterministic.GameFramework.Physics2D.Components;
using Deterministic.GameFramework.Types;
using FluentAssertions;
using Xunit;

namespace Deterministic.GameFramework.Physics.Tests;

public class PhysicsComponentTests
{
    [Fact]
    public void CircleShape2D_Properties_Work()
    {
        var circle = new CircleShape2D();
        circle.Radius = 5.0f;
        circle.Radius.Should().Be(5.0f);
        
        var circle2 = new CircleShape2D { Radius = 10.0f };
        circle2.Radius.Should().Be(10.0f);
    }

    [Fact]
    public void RectangleShape2D_Properties_Work()
    {
        var rect = new RectangleShape2D();
        rect.Size = new Vector2(10, 20);
        rect.Size.Should().Be(new Vector2(10, 20));
        
        var rect2 = new RectangleShape2D { Size = new Vector2(5, 5) };
        rect2.Size.Should().Be(new Vector2(5, 5));
    }

    [Fact]
    public void CapsuleShape2D_Properties_Work()
    {
        var cap = new CapsuleShape2D();
        cap.Radius = 1.0f;
        cap.Height = 2.0f;
        
        cap.Radius.Should().Be(1.0f);
        cap.Height.Should().Be(2.0f);
    }

    [Fact]
    public void CollisionShape2D_FactoryMethods_Work()
    {
        var circle = CollisionShape2D.CreateCircle(5.0f);
        circle.Type.Should().Be(CollisionShapeType.Circle);
        circle.Circle.Radius.Should().Be(5.0f);

        var rect = CollisionShape2D.CreateRectangle(new Vector2(10, 10));
        rect.Type.Should().Be(CollisionShapeType.Rectangle);
        rect.Rectangle.Size.Should().Be(new Vector2(10, 10));

        var cap = CollisionShape2D.CreateCapsule(1.0f, 2.0f);
        cap.Type.Should().Be(CollisionShapeType.Capsule);
        cap.Capsule.Radius.Should().Be(1.0f);
        cap.Capsule.Height.Should().Be(2.0f);
    }

    [Fact]
    public void CharacterBody2D_Properties_Work()
    {
        var body = new CharacterBody2D();
        body.Velocity = new Vector2(1, 1);
        body.UpDirection = Vector2.Up;
        body.FloorMaxAngle = Float.Pi / 4;
        body.WallMinSlideAngle = Float.Pi / 3;
        body.FloorSnapLength = 0.5f;
        // body.IsOnFloor = true;
        // body.IsOnWall = true;
        body.RealVelocity = new Vector2(2, 2);
        body.CollisionLayer = 1;
        body.CollisionMask = 2;
        body.BodyId = 123;

        body.Velocity.Should().Be(new Vector2(1, 1));
        body.UpDirection.Should().Be(Vector2.Up);
        //body.IsOnFloor.Should().BeTrue();
        body.RealVelocity.Should().Be(new Vector2(2, 2));
        body.BodyId.Should().Be(123);
        
        var def = CharacterBody2D.Default;
        def.UpDirection.Should().Be(Vector2.Up);
    }

    [Fact]
    public void RigidBody2D_Properties_Work()
    {
        var body = new RigidBody2D();
        body.BodyId = 100;
        body.Mass = 10;
        body.GravityScale = 2;
        body.LinearVelocity = new Vector2(1, 0);
        body.AngularVelocity = 5;
        body.LinearDamping = 0.1f;
        body.AngularDamping = 0.1f;
        body.CcdEnabled = true;
        body.CollisionLayer = 1;
        body.CollisionMask = 2;

        body.BodyId.Should().Be(100);
        body.Mass.Should().Be(10);
        body.CcdEnabled.Should().BeTrue();
        
        var def = RigidBody2D.Default;
        def.Mass.Should().Be(1);
    }

    [Fact]
    public void StaticBody2D_Properties_Work()
    {
        var body = new StaticBody2D();
        body.BodyId = 200;
        body.BodyId.Should().Be(200);
    }

    [Fact]
    public void Area2D_Properties_Work()
    {
        var area = new Area2D();
        area.BodyId = 300;
        area.Monitoring = true;
        area.CollisionLayer = 1;
        area.CollisionMask = 2;
        area.HasOverlappingBodies = true;
        
        area.BodyId.Should().Be(300);
        area.Monitoring.Should().BeTrue();
        area.HasOverlappingBodies.Should().BeTrue();
    }

    [Fact]
    public void CollisionShape2D_Equality_Works()
    {
        var shape1 = CollisionShape2D.CreateCircle(5.0f);
        var shape2 = CollisionShape2D.CreateCircle(5.0f);
        var shape3 = CollisionShape2D.CreateCircle(10.0f);
        var shape4 = CollisionShape2D.CreateRectangle(new Vector2(5, 5));

        // Equals
        shape1.Equals(shape2).Should().BeTrue();
        shape1.Equals((object)shape2).Should().BeTrue();
        shape1.Equals(shape3).Should().BeFalse();
        shape1.Equals(shape4).Should().BeFalse();
        shape1.Equals(null).Should().BeFalse();
        shape1.Equals("NotAShape").Should().BeFalse();

        // Operators
        (shape1 == shape2).Should().BeTrue();
        (shape1 != shape2).Should().BeFalse();
        (shape1 == shape3).Should().BeFalse();
        (shape1 != shape3).Should().BeTrue();

        // HashCode
        shape1.GetHashCode().Should().Be(shape2.GetHashCode());
        shape1.GetHashCode().Should().NotBe(shape3.GetHashCode());
    }

    [Fact]
    public void CollisionShapeType_Equality_Works()
    {
        var t1 = CollisionShapeType.Circle;
        var t2 = CollisionShapeType.Circle;
        var t3 = CollisionShapeType.Rectangle;

        t1.Equals(t2).Should().BeTrue();
        t1.Equals((object)t2).Should().BeTrue();
        t1.Equals(t3).Should().BeFalse();
        t1.Equals(null).Should().BeFalse();

        (t1 == t2).Should().BeTrue();
        (t1 != t2).Should().BeFalse();
        (t1 == t3).Should().BeFalse();
        (t1 != t3).Should().BeTrue();

        t1.GetHashCode().Should().Be(t2.GetHashCode());
        t1.GetHashCode().Should().NotBe(t3.GetHashCode());
        
        t1.ToString().Should().Be("Circle");
        t3.ToString().Should().Be("Rectangle");
        CollisionShapeType.Capsule.ToString().Should().Be("Capsule");
        CollisionShapeType.Segment.ToString().Should().Be("Segment");
        new CollisionShapeType(99).ToString().Should().Be("99");
    }

    [Fact]
    public void CircleShape2D_Equality_Works()
    {
        var c1 = new CircleShape2D { Radius = 5.0f };
        var c2 = new CircleShape2D { Radius = 5.0f };
        var c3 = new CircleShape2D { Radius = 10.0f };

        c1.Equals(c2).Should().BeTrue();
        c1.Equals((object)c2).Should().BeTrue();
        c1.Equals(c3).Should().BeFalse();
        c1.Equals(null).Should().BeFalse();
        c1.Equals("NotACircle").Should().BeFalse();

        (c1 == c2).Should().BeTrue();
        (c1 != c2).Should().BeFalse();
        (c1 == c3).Should().BeFalse();
        (c1 != c3).Should().BeTrue();

        c1.GetHashCode().Should().Be(c2.GetHashCode());
        c1.GetHashCode().Should().NotBe(c3.GetHashCode());
    }

    [Fact]
    public void RectangleShape2D_Equality_Works()
    {
        var r1 = new RectangleShape2D { Size = new Vector2(10, 10) };
        var r2 = new RectangleShape2D { Size = new Vector2(10, 10) };
        var r3 = new RectangleShape2D { Size = new Vector2(5, 5) };

        r1.Equals(r2).Should().BeTrue();
        r1.Equals((object)r2).Should().BeTrue();
        r1.Equals(r3).Should().BeFalse();
        r1.Equals(null).Should().BeFalse();
        r1.Equals("NotARect").Should().BeFalse();

        (r1 == r2).Should().BeTrue();
        (r1 != r2).Should().BeFalse();
        (r1 == r3).Should().BeFalse();
        (r1 != r3).Should().BeTrue();

        r1.GetHashCode().Should().Be(r2.GetHashCode());
        r1.GetHashCode().Should().NotBe(r3.GetHashCode());
    }

    [Fact]
    public void CapsuleShape2D_Equality_Works()
    {
        var c1 = new CapsuleShape2D { Radius = 1.0f, Height = 2.0f };
        var c2 = new CapsuleShape2D { Radius = 1.0f, Height = 2.0f };
        var c3 = new CapsuleShape2D { Radius = 2.0f, Height = 2.0f }; // Diff Radius
        var c4 = new CapsuleShape2D { Radius = 1.0f, Height = 3.0f }; // Diff Height

        c1.Equals(c2).Should().BeTrue();
        c1.Equals((object)c2).Should().BeTrue();
        c1.Equals(c3).Should().BeFalse();
        c1.Equals(c4).Should().BeFalse();
        c1.Equals(null).Should().BeFalse();
        c1.Equals("NotACapsule").Should().BeFalse();

        (c1 == c2).Should().BeTrue();
        (c1 != c2).Should().BeFalse();
        (c1 == c3).Should().BeFalse();
        (c1 != c3).Should().BeTrue();

        c1.GetHashCode().Should().Be(c2.GetHashCode());
        c1.GetHashCode().Should().NotBe(c3.GetHashCode());
    }

    [Fact]
    public void PlatformOnLeave_Equality_Works()
    {
        var p1 = PlatformOnLeave.AddVelocity;
        var p2 = PlatformOnLeave.AddVelocity;
        var p3 = PlatformOnLeave.DoNothing;
        var p4 = new PlatformOnLeave(99);

        p1.Equals(p2).Should().BeTrue();
        p1.Equals((object)p2).Should().BeTrue();
        p1.Equals(p3).Should().BeFalse();
        p1.Equals(null).Should().BeFalse();

        (p1 == p2).Should().BeTrue();
        (p1 != p2).Should().BeFalse();
        (p1 == p3).Should().BeFalse();
        (p1 != p3).Should().BeTrue();

        p1.GetHashCode().Should().Be(p2.GetHashCode());
        p1.GetHashCode().Should().NotBe(p3.GetHashCode());

        p1.ToString().Should().Be("AddVelocity");
        p3.ToString().Should().Be("DoNothing");
        p4.ToString().Should().Be("99");
    }
}
