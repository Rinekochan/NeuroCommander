using Godot;
using System;
using NeuroWarCommander.Scripts.Team.Blackboard;
using NeuroWarCommander.Scripts.Utils;

namespace NeuroWarCommander.Scripts.Units.Base;

public partial class UnitBase : CharacterBody2D
{
    [Signal]
    public delegate void UnitDamagedEventHandler(float damage, Node2D source, Node2D target);

    [Signal]
    public delegate void UnitHealedEventHandler(float amount, Node2D source, Node2D target);

    [Signal]
    public delegate void UnitDestroyedEventHandler(Node2D source, Vector2 position);

    [Export] public float MaxHealth { get; set; } = 100.0f;
    [Export] public float CurrentHealth { get; set; }
    [Export] public int TeamId { get; set; } = 0;

    public bool IsDebugging = false; // Used to toggle debug visualisations
    public bool ShowHealthBar = true;
    public bool ShowPerceptionSystemVisualisation = false;
    public bool ShowSteeringSystemVisualisation = false;
    public bool ShowPathfindingSystemVisualisation = false;

    public override void _EnterTree()
    {
        IsDebugging = false; // Used to toggle debug visualisations
        ShowHealthBar = true;
        ShowPerceptionSystemVisualisation = false;
        ShowSteeringSystemVisualisation = false;
        ShowPathfindingSystemVisualisation = false;
    }

    public override void _Ready()
    {
        CurrentHealth = MaxHealth;

        SetMap(null);
    }

    public override void _Process(double delta)
    {
        QueueRedraw();
    }

    public void SetMap(Node mapNode)
    {
        var steeringSystem = GetNode<SteeringSystem>("Steering");

        var map = mapNode as Map;

        if (map == null)
        {
            map = GetTree().Root.GetNode<Map>("Game/World/Map") ??
                   GetTree().Root.GetNode<Map>("World/Map");
        }

        // Steering system checks on terrain, the grid won't be exposed to the unit
        if (steeringSystem != null)
        {
            GD.Print("Setting map reference for steering system: " + map);
            steeringSystem.SetMapReference(map);
        }

        var pathfindingSystem = GetNode<PathfindingSystem>("Pathfinding");

        pathfindingSystem?.SetGrid(map.GetGrid());
    }

    public override void _PhysicsProcess(double delta)
    {
        MoveAndSlide();
        if(Velocity != Vector2I.Zero) Velocity -= Velocity.Normalized() * 2.0f; // Apply some friction to the unit's movement

        // Prevent units from going off-screen
        if (GlobalPosition.X > 1024) GlobalPosition = new Vector2(1024, GlobalPosition.Y);
        if (GlobalPosition.Y > 1024) GlobalPosition = new Vector2(GlobalPosition.X, 1024);
        if (GlobalPosition.X < -1024) GlobalPosition = new Vector2(-1024, GlobalPosition.Y);
        if (GlobalPosition.Y < -1024) GlobalPosition = new Vector2(GlobalPosition.X, -1024);
    }

    public virtual bool TakeDamage(float damage, Node2D source)
    {
        float oldHealth = CurrentHealth;
        CurrentHealth = Mathf.Max(0, CurrentHealth - damage);

        EmitSignal(SignalName.UnitDamaged, damage, source, this);

        if (oldHealth > 0 && CurrentHealth <= 0)
        {
            // Emit the signal that the unit was destroyed
            EmitSignal(SignalName.UnitDestroyed, this, GlobalPosition);
            Die();
            return true;
        }

        return false;
    }

    public bool Heal(float amount, Node2D source)
    {
        if (CurrentHealth <= 0 || CurrentHealth >= MaxHealth) return false;

        float oldHealth = CurrentHealth;
        CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);

        if(CurrentHealth == oldHealth) return false; // No change in health

        EmitSignal(SignalName.UnitHealed, CurrentHealth - oldHealth, source, this);
        return true;
    }

    protected virtual void Die()
    {
        // Disconnect all signals from perception systems
        var visionCircle = GetNodeOrNull<PerceptionSystem>("VisionCircle");
        if (visionCircle != null)
            visionCircle.DisconnectAllSignals();

        var fovCone = GetNodeOrNull<PerceptionSystem>("FOVCone");
        if (fovCone != null)
            fovCone.DisconnectAllSignals();

        // Disconnect all signals from the FSM
        var fsm = GetNodeOrNull<UnitFSM>("UnitFSM");
        fsm.TransitionToState(UnitFSM.State.Dead);

        // Clean up any other references or connections
        var pathfindingSystem = GetNodeOrNull<PathfindingSystem>("Pathfinding");
        if (pathfindingSystem != null)
            pathfindingSystem.ClearPathVisualization();

        // Get the blackboard node
        var blackboard = GetParent().GetParent().GetNode<Blackboard>("Blackboard");

        // Then disconnect all signals from the unit itself
        Disconnect(nameof(UnitDamaged), new Callable(blackboard, nameof(Blackboard.OnUnitDamaged)));
        Disconnect(nameof(UnitDestroyed), new Callable(blackboard, nameof(Blackboard.OnUnitDestroyed)));
        Disconnect(nameof(UnitHealed), new Callable(blackboard, nameof(Blackboard.OnUnitHealed)));

        // Queue the node for deletion
        QueueFree();
    }

    public override void _Draw()
    {
        if (!IsDebugging || !ShowHealthBar) return;

        // Draw health bar circle
        float healthPercent = CurrentHealth / MaxHealth;
        Vector2 healthBarSize = new Vector2(40, 10);
        Vector2 healthBarPos = ToLocal(GlobalPosition) + new Vector2(-healthBarSize.X / 2, 50);

        // Health bar background
        DrawRect(new Rect2(healthBarPos, healthBarSize), new Color(0.2f, 0.2f, 0.2f, 0.8f));

        // Health bar fill
        DrawRect(new Rect2(healthBarPos, new Vector2(healthBarSize.X * healthPercent, healthBarSize.Y)), new Color(0, 0.8f, 0, 0.8f));
    }
}