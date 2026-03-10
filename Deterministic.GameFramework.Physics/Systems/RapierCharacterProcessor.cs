using System.Collections.Generic;
using Deterministic.GameFramework.CoreV2;
using Deterministic.GameFramework.CoreV2.Components;
using Deterministic.GameFramework.Physics.Components;
using uniffi.rapier_uniffi;

namespace Deterministic.GameFramework.Physics.Systems;

internal class RapierCharacterProcessor
{
    // EntityId -> CharacterController
    private readonly Dictionary<int, RapierCharacterController> _entityToController = new();

    public void StepCharacters(GlobalState state, RapierWorld world, Dictionary<int, ulong> entityToBody)
    {
        if (world == null) return;
        var dt = (float)state.GameLoop.FixedDeltaTime;

        foreach (var entity in state.Filter<CharacterBody2D, Transform2D, CollisionShape2D>())
        {
            ref var character = ref state.GetComponent<CharacterBody2D>(entity);
            ref var transform = ref state.GetComponent<Transform2D>(entity);
            ref var shapeComp = ref state.GetComponent<CollisionShape2D>(entity);

            // Get or Create Controller
            if (!_entityToController.TryGetValue(entity.Id, out var controller))
            {
                controller = new RapierCharacterController(0.01f); // Default offset
                _entityToController[entity.Id] = controller;
            }

            // Update Controller Settings
            controller.SetUp(new RVector((float)character.UpDirection.X, (float)character.UpDirection.Y));
            controller.SetMaxSlopeClimbAngle((float)character.FloorMaxAngle);
            controller.SetMinSlopeSlideAngle((float)character.WallMinSlideAngle);
            controller.SetSlideEnabled(true);
            controller.SetSnapToGround((float)character.FloorSnapLength);

            // Prepare Shape
            RapierShape? shape = null;
            if (shapeComp.Type == CollisionShapeType.Circle)
                shape = RapierShape.Ball((float)shapeComp.Circle.Radius);
            else if (shapeComp.Type == CollisionShapeType.Rectangle)
                shape = RapierShape.Cuboid((float)shapeComp.Rectangle.Size.X / 2.0f, (float)shapeComp.Rectangle.Size.Y / 2.0f);
            else if (shapeComp.Type == CollisionShapeType.Capsule)
                shape = RapierShape.Capsule((float)shapeComp.Capsule.Height / 2.0f, (float)shapeComp.Capsule.Radius);

            if (shape == null) continue;

            // Prepare Movement
            var shapePos = new RVector((float)transform.GlobalPosition.X + (float)shapeComp.Position.X, (float)transform.GlobalPosition.Y + (float)shapeComp.Position.Y);
            var shapeRot = new RRotation((float)transform.GlobalRotation + (float)shapeComp.Rotation);
            
            // Disable collision with self if body exists
            bool hasBody = entityToBody.TryGetValue(entity.Id, out ulong bodyHandle);
            
            if (hasBody)
            {
                world.BodySetEnabled(bodyHandle, false);
            }

            // Desired translation based on Velocity * dt
            var desiredTranslation = new RVector((float)character.Velocity.X * dt, (float)character.Velocity.Y * dt);
            
            // Move
            // uint.MaxValue for all layers collision for now
            var result = controller.MoveShape(dt, world, shape, shapePos, shapeRot, desiredTranslation, uint.MaxValue);
            
            // Update Transform
            var newPos = new Vector2(shapePos.x + result.translation.x - (float)shapeComp.Position.X, shapePos.y + result.translation.y - (float)shapeComp.Position.Y);
            transform.GlobalPosition = newPos;
            
            if (transform.Parent == Entity.Null)
            {
                transform.Position = transform.GlobalPosition;
            }
            
            // Re-enable body and update position
            if (hasBody)
            {
                world.BodySetEnabled(bodyHandle, true);
                
                // Update body to new position immediately so other characters can collide with it
                var newBodyPos = new RVector(shapePos.x + result.translation.x, shapePos.y + result.translation.y);
                var newBodyRot = shapeRot; 
                
                world.BodySetTranslation(bodyHandle, newBodyPos, true);
                world.BodySetRotation(bodyHandle, newBodyRot, true);
                
                // Set velocity for completeness
                if (dt > 0)
                {
                    world.BodySetLinvel(bodyHandle, new RVector(result.translation.x / dt, result.translation.y / dt), true);
                }
            }
            
            // Update Character State
            character.IsOnFloor = result.grounded;
            
            // Calculate Real Velocity
            if (dt > 0)
            {
                character.RealVelocity = new Vector2(result.translation.x / dt, result.translation.y / dt);
            }
            else
            {
                character.RealVelocity = Vector2.Zero;
            }

            shape.Dispose();
        }
    }

    public void Clear()
    {
        // Stateless, nothing to clear
    }
}
