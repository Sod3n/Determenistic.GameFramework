using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.TwoD;
using Deterministic.GameFramework.Common;
using Deterministic.GameFramework.Types;
using FluentAssertions;
using Xunit;

namespace Deterministic.GameFramework.TwoD.Tests;

[Collection("Sequential")]
public class TransformTests
{
    private (EntityWorld state, TransformSystem system) CreateWorld()
    {
        ServiceLocator.Reset();
        ServiceLocator.RegisterAssembly(typeof(EntityWorld).Assembly);
        ServiceLocator.RegisterAssembly(typeof(Transform2D).Assembly);

        var state = new EntityWorld();
        state.RegisterComponent<Transform2D>();
        
        var system = new TransformSystem();
        
        return (state, system);
    }

    [Fact]
    public void RootEntity_LocalAndGlobal_AreSynced()
    {
        var (state, system) = CreateWorld();
        var entity = state.CreateEntity();
        
        var transform = new Transform2D(new Vector2(10, 20), 0, Vector2.One);
        state.AddComponent(entity, transform);
        
        system.Update(state);
        
        var updated = state.GetComponent<Transform2D>(entity);
        updated.GlobalPosition.Should().Be(new Vector2(10, 20));
        updated.Position.Should().Be(new Vector2(10, 20));
    }

    [Fact]
    public void ChildEntity_InheritsTransform_FromParent()
    {
        var (state, system) = CreateWorld();
        
        // Parent at (10, 0)
        var parent = state.CreateEntity();
        state.AddComponent(parent, new Transform2D(new Vector2(10, 0), 0, Vector2.One));
        
        // Child at (5, 0) local -> Expect (15, 0) global
        var child = state.CreateEntity();
        var childTransform = new Transform2D(new Vector2(5, 0), 0, Vector2.One);
        childTransform.Parent = parent;
        state.AddComponent(child, childTransform);
        
        system.Update(state);
        
        var updatedChild = state.GetComponent<Transform2D>(child);
        updatedChild.GlobalPosition.Should().Be(new Vector2(15, 0));
    }

    [Fact]
    public void ChildEntity_RotatedParent_RotatesChildGlobalPosition()
    {
        var (state, system) = CreateWorld();
        
        // Parent at (0, 0), Rotated 90 degrees (PI/2)
        var parent = state.CreateEntity();
        state.AddComponent(parent, new Transform2D(Vector2.Zero, Float.Pi / 2, Vector2.One));
        
        // Child at (10, 0) local. 
        // Rotated by 90 deg around (0,0) -> Should be at (0, 10)
        var child = state.CreateEntity();
        var childTransform = new Transform2D(new Vector2(10, 0), 0, Vector2.One);
        childTransform.Parent = parent;
        state.AddComponent(child, childTransform);
        
        system.Update(state);
        
        var updatedChild = state.GetComponent<Transform2D>(child);
        
        // Float precision might require tolerance
        ((float)updatedChild.GlobalPosition.X).Should().BeApproximately(0, 0.001f);
        ((float)updatedChild.GlobalPosition.Y).Should().BeApproximately(10, 0.001f);
        updatedChild.GlobalRotation.Should().Be(Float.Pi / 2);
    }

    [Fact]
    public void ChildEntity_SettingGlobalPosition_UpdatesLocalPosition()
    {
        var (state, system) = CreateWorld();
        
        // Parent at (10, 0)
        var parent = state.CreateEntity();
        state.AddComponent(parent, new Transform2D(new Vector2(10, 0), 0, Vector2.One));
        
        // Child initialized at (0,0) global/local
        var child = state.CreateEntity();
        var childTransform = new Transform2D(Vector2.Zero, 0, Vector2.One);
        childTransform.Parent = parent;
        
        // Set Desired Global Position to (20, 0)
        // Since Parent is at (10, 0), Local should become (10, 0)
        childTransform.GlobalPosition = new Vector2(20, 0);
        state.AddComponent(child, childTransform);
        
        system.Update(state);
        
        var updatedChild = state.GetComponent<Transform2D>(child);
        updatedChild.Position.Should().Be(new Vector2(10, 0));
        updatedChild.GlobalPosition.Should().Be(new Vector2(20, 0));
    }

    [Fact]
    public void MovingParent_MovesChild()
    {
        var (state, system) = CreateWorld();
        
        var parent = state.CreateEntity();
        state.AddComponent(parent, new Transform2D(Vector2.Zero, 0, Vector2.One));
        
        var child = state.CreateEntity();
        var childTransform = new Transform2D(new Vector2(5, 0), 0, Vector2.One);
        childTransform.Parent = parent;
        state.AddComponent(child, childTransform);
        
        // Update 1: Init
        system.Update(state);
        
        // Move Parent to (10, 10)
        ref var pTrans = ref state.GetComponent<Transform2D>(parent);
        pTrans.GlobalPosition = new Vector2(10, 10); // Or Position, since root
        
        // Update 2: Propagate
        system.Update(state);
        
        var cTrans = state.GetComponent<Transform2D>(child);
        cTrans.GlobalPosition.Should().Be(new Vector2(15, 10)); // (10+5, 10+0)
    }

    [Fact]
    public void OrphanWithDestroyFlag_ShouldBeDestroyed_WhenParentIsDeleted()
    {
        var (state, system) = CreateWorld();
        
        var parent = state.CreateEntity();
        state.AddComponent(parent, new Transform2D(Vector2.Zero, 0, Vector2.One));
        
        var child = state.CreateEntity();
        var childTransform = new Transform2D(Vector2.Zero, 0, Vector2.One);
        childTransform.Parent = parent;
        childTransform.DestroyOnUnparent = true;
        state.AddComponent(child, childTransform);
        
        system.Update(state);
        
        // Delete Parent
        state.DeleteEntity(parent);
        
        // Update to process destruction
        system.Update(state);
        
        // Child should be gone
        state.HasComponent<Transform2D>(child).Should().BeFalse();
        // Mask check
        state.EntityMasks[child.Id].IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void OrphanWithoutDestroyFlag_ShouldBecomeRoot_WhenParentIsDeleted()
    {
        var (state, system) = CreateWorld();
        
        var parent = state.CreateEntity();
        state.AddComponent(parent, new Transform2D(new Vector2(10, 0), 0, Vector2.One));
        
        var child = state.CreateEntity();
        var childTransform = new Transform2D(new Vector2(5, 0), 0, Vector2.One); // Global (15, 0)
        childTransform.Parent = parent;
        childTransform.DestroyOnUnparent = false;
        state.AddComponent(child, childTransform);
        
        system.Update(state);
        
        // Verify init
        state.GetComponent<Transform2D>(child).GlobalPosition.Should().Be(new Vector2(15, 0));
        
        // Delete Parent
        state.DeleteEntity(parent);
        
        // Update
        system.Update(state);
        
        // Child should exist
        state.HasComponent<Transform2D>(child).Should().BeTrue();
        
        var cTrans = state.GetComponent<Transform2D>(child);
        cTrans.Parent.Should().Be(Entity.Null);
        
        // Position should be preserved relative to world?
        // TransformSystem calculates Global from Local if root.
        // If it becomes root, its Local Position (5, 0) becomes Global Position (5, 0).
        // UNLESS the system explicitly handles "Detachment" by converting Global back to Local relative to new parent (null).
        // Looking at code:
        // "childTransform.Parent = Entity.Null; state.AddComponent(...);"
        // It does NOT update Position.
        // So Local Position remains (5, 0).
        // Next update: Root logic runs. Global = Local = (5, 0).
        // So it "jumps" to local coordinates. This is standard behavior if not handling "KeepWorldPosition".
        
        cTrans.GlobalPosition.Should().Be(new Vector2(5, 0));
    }

    [Fact]
    public void GrandChild_InheritsTransform_Recursively()
    {
        var (state, system) = CreateWorld();

        // Parent (10, 0)
        var parent = state.CreateEntity();
        state.AddComponent(parent, new Transform2D(new Vector2(10, 0), 0, Vector2.One));

        // Child (5, 0) -> Global (15, 0)
        var child = state.CreateEntity();
        var childTransform = new Transform2D(new Vector2(5, 0), 0, Vector2.One);
        childTransform.Parent = parent;
        state.AddComponent(child, childTransform);

        // Grandchild (2, 0) -> Global (17, 0)
        var grandChild = state.CreateEntity();
        var grandChildTransform = new Transform2D(new Vector2(2, 0), 0, Vector2.One);
        grandChildTransform.Parent = child;
        state.AddComponent(grandChild, grandChildTransform);

        system.Update(state);

        var updatedGrandChild = state.GetComponent<Transform2D>(grandChild);
        updatedGrandChild.GlobalPosition.Should().Be(new Vector2(17, 0));
    }

    [Fact]
    public void Scale_Propagates_Correctly()
    {
        var (state, system) = CreateWorld();

        // Parent Scale (2, 2)
        var parent = state.CreateEntity();
        state.AddComponent(parent, new Transform2D(Vector2.Zero, 0, new Vector2(2, 2)));

        // Child Local (5, 0) -> Scaled by Parent -> Global (10, 0)
        var child = state.CreateEntity();
        var childTransform = new Transform2D(new Vector2(5, 0), 0, Vector2.One);
        childTransform.Parent = parent;
        state.AddComponent(child, childTransform);

        system.Update(state);

        var updatedChild = state.GetComponent<Transform2D>(child);
        updatedChild.GlobalPosition.Should().Be(new Vector2(10, 0));
        updatedChild.GlobalScale.Should().Be(new Vector2(2, 2));
    }

    [Fact]
    public void GlobalPosition_SetOnChild_UpdatesLocal_WithRotationAndScale()
    {
        var (state, system) = CreateWorld();

        // Parent at (10, 0), Rotated 90 deg, Scale (2, 2)
        var parent = state.CreateEntity();
        state.AddComponent(parent, new Transform2D(new Vector2(10, 0), Float.Pi / 2, new Vector2(2, 2)));

        // Child initially at zero
        var child = state.CreateEntity();
        var childTransform = new Transform2D(Vector2.Zero, 0, Vector2.One);
        childTransform.Parent = parent;
        
        // We want Child Global to be at (10, 10)
        // Parent Global is (10, 0)
        // Relative Global Vector is (0, 10)
        // Unscaled by (2, 2) -> (0, 5)
        // Unrotated by 90 deg (Clockwise) -> (5, 0)
        // So Local Position should be (5, 0)
        childTransform.GlobalPosition = new Vector2(10, 10);
        state.AddComponent(child, childTransform);

        system.Update(state);

        var updatedChild = state.GetComponent<Transform2D>(child);
        
        ((float)updatedChild.Position.X).Should().BeApproximately(5, 0.001f);
        ((float)updatedChild.Position.Y).Should().BeApproximately(0, 0.001f);
    }

    [Fact]
    public void Grandchild_Destruction_Propagation()
    {
        var (state, system) = CreateWorld();

        // A -> B -> C
        // A: Parent
        // B: Child (DestroyOnUnparent = true)
        // C: Grandchild (DestroyOnUnparent = true)

        var entityA = state.CreateEntity();
        state.AddComponent(entityA, new Transform2D(Vector2.Zero, 0, Vector2.One));

        var entityB = state.CreateEntity();
        var transformB = new Transform2D(Vector2.Zero, 0, Vector2.One);
        transformB.Parent = entityA;
        transformB.DestroyOnUnparent = true;
        state.AddComponent(entityB, transformB);

        var entityC = state.CreateEntity();
        var transformC = new Transform2D(Vector2.Zero, 0, Vector2.One);
        transformC.Parent = entityB;
        transformC.DestroyOnUnparent = true;
        state.AddComponent(entityC, transformC);

        system.Update(state);

        // Delete A
        state.DeleteEntity(entityA);

        // Update
        // 1. B detects A is gone -> B is orphan -> B added to destroy list.
        // 2. Propagate: B is in destroy list -> Find children of B (C).
        // 3. C is DestroyOnUnparent -> C added to destroy list.
        system.Update(state);

        state.HasComponent<Transform2D>(entityB).Should().BeFalse();
        state.HasComponent<Transform2D>(entityC).Should().BeFalse();
    }

    [Fact]
    public void Grandchild_Survival_When_Parent_Destroyed()
    {
        var (state, system) = CreateWorld();

        // A -> B -> C
        // A: Parent
        // B: Child (DestroyOnUnparent = true)
        // C: Grandchild (DestroyOnUnparent = false) -> Should survive and become root

        var entityA = state.CreateEntity();
        state.AddComponent(entityA, new Transform2D(Vector2.Zero, 0, Vector2.One));

        var entityB = state.CreateEntity();
        var transformB = new Transform2D(Vector2.Zero, 0, Vector2.One);
        transformB.Parent = entityA;
        transformB.DestroyOnUnparent = true;
        state.AddComponent(entityB, transformB);

        var entityC = state.CreateEntity();
        var transformC = new Transform2D(new Vector2(10, 10), 0, Vector2.One);
        transformC.Parent = entityB;
        transformC.DestroyOnUnparent = false;
        state.AddComponent(entityC, transformC);

        system.Update(state);

        // Delete A
        state.DeleteEntity(entityA);

        // Update
        system.Update(state);

        state.HasComponent<Transform2D>(entityB).Should().BeFalse();
        state.HasComponent<Transform2D>(entityC).Should().BeTrue();
        
        var cTrans = state.GetComponent<Transform2D>(entityC);
        cTrans.Parent.Should().Be(Entity.Null);
    }

    [Fact]
    public void Transform2D_Default_ReturnsIdentity()
    {
        var def = Transform2D.Default;
        def.Position.Should().Be(Vector2.Zero);
        def.Rotation.Should().Be(0);
        def.Scale.Should().Be(Vector2.One);
        def.GlobalPosition.Should().Be(Vector2.Zero);
        def.GlobalRotation.Should().Be(0);
        def.GlobalScale.Should().Be(Vector2.One);
        def.Parent.Should().Be(Entity.Null);
    }
}
