using Deterministic.GameFramework.Box2D;
using static Deterministic.GameFramework.Box2D.B2Types;
using static Deterministic.GameFramework.Box2D.B2Worlds;
using static Deterministic.GameFramework.Box2D.B2Bodies;
using static Deterministic.GameFramework.Box2D.B2Shapes;
using static Deterministic.GameFramework.Box2D.B2Geometries;
using static Deterministic.GameFramework.Box2D.B2MathFunction;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

using Float = Deterministic.GameFramework.Types.Float;

namespace Deterministic.GameFramework.Physics.Tests;

public class Box2DSensorTest
{
    private readonly ITestOutputHelper _output;
    public Box2DSensorTest(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Sensor_DetectsOverlap_WhenDynamicBodyEnters()
    {
        var worldDef = b2DefaultWorldDef();
        worldDef.gravity = new B2Vec2(0, 0);
        var worldId = b2CreateWorld(worldDef);

        // Create sensor body (dynamic, sensor shape)
        var sensorBodyDef = b2DefaultBodyDef();
        sensorBodyDef.type = B2BodyType.b2_dynamicBody;
        sensorBodyDef.position = new B2Vec2(0, 0);
        sensorBodyDef.gravityScale = 0;
        var sensorBody = b2CreateBody(worldId, sensorBodyDef);

        var sensorShapeDef = b2DefaultShapeDef();
        sensorShapeDef.isSensor = true;
        sensorShapeDef.enableSensorEvents = true;
        sensorShapeDef.filter = new B2Filter(8, 5, 0); // Zone layer, detects Physics|Interactable
        var sensorCircle = new B2Circle(new B2Vec2(0, 0), 2.0f);
        b2CreateCircleShape(sensorBody, sensorShapeDef, sensorCircle);

        // Create moving body (dynamic, starts far away)
        var moverBodyDef = b2DefaultBodyDef();
        moverBodyDef.type = B2BodyType.b2_dynamicBody;
        moverBodyDef.position = new B2Vec2(10, 0);
        moverBodyDef.gravityScale = 0;
        var moverBody = b2CreateBody(worldId, moverBodyDef);

        var moverShapeDef = b2DefaultShapeDef();
        moverShapeDef.enableSensorEvents = true;
        moverShapeDef.filter = new B2Filter(1, 8, 0); // Physics layer, detects Zone
        var moverCircle = new B2Circle(new B2Vec2(0, 0), 0.5f);
        b2CreateCircleShape(moverBody, moverShapeDef, moverCircle);

        // Step a few times — no overlap expected
        for (int i = 0; i < 5; i++)
            b2World_Step(worldId, 1.0f / 60.0f, 4);

        var events1 = b2World_GetSensorEvents(worldId);
        _output.WriteLine($"Before move: begin={events1.beginCount} end={events1.endCount}");
        events1.beginCount.Should().Be(0, "no overlap yet");

        // Move mover into sensor range
        b2Body_SetTransform(moverBody, new B2Vec2(1, 0), b2MakeRot(0));
        b2Body_SetAwake(moverBody, true);
        b2Body_SetAwake(sensorBody, true);

        b2World_Step(worldId, 1.0f / 60.0f, 4);

        var events2 = b2World_GetSensorEvents(worldId);
        _output.WriteLine($"After move into range: begin={events2.beginCount} end={events2.endCount}");
        events2.beginCount.Should().BeGreaterThan(0, "mover is inside sensor radius");

        // Move mover out of range
        b2Body_SetTransform(moverBody, new B2Vec2(10, 0), b2MakeRot(0));
        b2Body_SetAwake(moverBody, true);

        b2World_Step(worldId, 1.0f / 60.0f, 4);

        var events3 = b2World_GetSensorEvents(worldId);
        _output.WriteLine($"After move out: begin={events3.beginCount} end={events3.endCount}");
        events3.endCount.Should().BeGreaterThan(0, "mover left sensor radius");

        b2DestroyWorld(worldId);
    }

    [Fact]
    public void Sensor_DetectsOverlap_WithStaticBody()
    {
        var worldDef = b2DefaultWorldDef();
        worldDef.gravity = new B2Vec2(0, 0);
        var worldId = b2CreateWorld(worldDef);

        // Static body (building/interactable)
        var staticBodyDef = b2DefaultBodyDef();
        staticBodyDef.type = B2BodyType.b2_staticBody;
        staticBodyDef.position = new B2Vec2(0, 0);
        var staticBody = b2CreateBody(worldId, staticBodyDef);

        var staticShapeDef = b2DefaultShapeDef();
        staticShapeDef.enableSensorEvents = true;
        staticShapeDef.filter = new B2Filter(4, 8, 0); // Interactable, detects Zone
        var box = b2MakeBox(1, 1);
        b2CreatePolygonShape(staticBody, staticShapeDef, box);

        // Dynamic sensor (player zone) — starts far
        var sensorBodyDef = b2DefaultBodyDef();
        sensorBodyDef.type = B2BodyType.b2_dynamicBody;
        sensorBodyDef.position = new B2Vec2(10, 0);
        sensorBodyDef.gravityScale = 0;
        var sensorBody = b2CreateBody(worldId, sensorBodyDef);

        var sensorShapeDef = b2DefaultShapeDef();
        sensorShapeDef.isSensor = true;
        sensorShapeDef.enableSensorEvents = true;
        sensorShapeDef.filter = new B2Filter(8, 5, 0); // Zone, detects Physics|Interactable
        var sensorCircle = new B2Circle(new B2Vec2(0, 0), 2.0f);
        b2CreateCircleShape(sensorBody, sensorShapeDef, sensorCircle);

        // Step
        b2World_Step(worldId, 1.0f / 60.0f, 4);
        var events1 = b2World_GetSensorEvents(worldId);
        _output.WriteLine($"Far away: begin={events1.beginCount}");
        events1.beginCount.Should().Be(0);

        // Move sensor near static body
        b2Body_SetTransform(sensorBody, new B2Vec2(0, 0), b2MakeRot(0));
        b2Body_SetAwake(sensorBody, true);

        b2World_Step(worldId, 1.0f / 60.0f, 4);
        var events2 = b2World_GetSensorEvents(worldId);
        _output.WriteLine($"Overlapping: begin={events2.beginCount}");
        events2.beginCount.Should().BeGreaterThan(0, "sensor overlaps static body");

        b2DestroyWorld(worldId);
    }
}
