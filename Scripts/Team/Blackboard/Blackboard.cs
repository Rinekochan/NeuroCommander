using Godot;
using System;
using System.Collections.Generic;
using NeuroWarCommander.Scripts.Camps;
using NeuroWarCommander.Scripts.Units.Base;
using NeuroWarCommander.Scripts.Units.Base.AttackableBase;
using NeuroWarCommander.Scripts.Utils;

namespace NeuroWarCommander.Scripts.Team.Blackboard;

// We will maintains a small “event bus” of ephemeral events (damage, heal, weapon‐fire, target‐capture), which are stamped with game‐time.

// Event types we’ll enqueue in the bus:
public enum TacticalEventType
{
    UnitDamaged,
    UnitHealed,
    UnitDestroyed,
    WeaponFired,
    WeaponEmpty,
    ReloadStarted,
    ReloadCompleted,
    TargetCaptured
}

// A simple struct to hold one bus entry:
public struct TacticalEvent
{
    public float Timestamp; // game time at which this occurred
    public TacticalEventType Type; // which kind
    public string Source; // e.g. the unit that was damaged or fired
    public string Target; // for WeaponFired/TargetAcquired, the target unit
    public float Amount; // HP for UnitDamaged/UnitHealed. Ammo for WeaponFired

    public TacticalEvent(float time, TacticalEventType type, string source = null, string target = null, float amount = 0f)
    {
        Timestamp = time;
        Type = type;
        Source = source;
        Target = target;
        Amount = amount;
    }
}

public partial class Blackboard : Node
{

    // Event bus fields
    // Each ephemeral event will have their own TTL (time-to-live) in seconds
    [Export] public float EventTTL = 5.0f;
    [Export] public int TeamId = 0;

    // Signals for researching purposes
    [Signal] public delegate void EnemyDetectedEventHandler();
    [Signal] public delegate void WeaponFiredEventHandler();

    private float _gameTime;

    // A FIFO list of recent tactical events:
    private List<TacticalEvent> _eventBus = new();

    // References to each map under Blackboard
    private VisionMap _visionMap;
    private LocationMap _locationMap;
    private TerrainMap _terrainMap;
    private InfluenceMap _influenceMap;
    private Team _team;

    public override void _Ready()
    {
        _team = GetParent<Team>();
        _visionMap = GetNode<VisionMap>("VisionMap");
        _locationMap = GetNode<LocationMap>("LocationMap");
        _terrainMap = GetNode<TerrainMap>("TerrainMap");
        _influenceMap = GetNode<InfluenceMap>("InfluenceMap");

        CallDeferred(nameof(Initialize));
    }
    private void Initialize()
    {
        // For each unit under Team/Units, connect that unit’s PerceptionSystem signals
        var unitsRoot = GetParent().GetNode<Node>("Units");

        GD.Print($"Blackboard ready for Team {TeamId} with {unitsRoot.GetChildCount()} units.");
        foreach (Node child in unitsRoot.GetChildren())
        {
            if (child is Node2D unitNode)
            {
                // Connect VisionCircle if present
                var visionCircle = unitNode.GetNodeOrNull<PerceptionSystem>("VisionCircle");
                if (visionCircle != null)
                    ConnectPerceptionSignals(visionCircle);

                // Connect FOVCone if present
                var fovCone = unitNode.GetNodeOrNull<PerceptionSystem>("FOVCone");
                if (fovCone != null)
                    ConnectPerceptionSignals(fovCone);

                var baseScript = unitNode as UnitBase;
                if (baseScript != null)
                {
                    baseScript.Connect(
                        nameof(UnitBase.UnitDamaged),
                        new Callable(this, nameof(OnUnitDamaged))
                    );
                    baseScript.Connect(
                        nameof(UnitBase.UnitHealed),
                        new Callable(this, nameof(OnUnitHealed))
                    );
                    baseScript.Connect(
                        nameof(UnitBase.UnitDestroyed),
                        new Callable(this, nameof(OnUnitDestroyed))
                    );
                }

                // Connect AttackableUnitBase signals—also bus for some, immediate for others
                var attackable = unitNode as AttackableUnitBase;
                if (attackable != null)
                {
                    attackable.Connect(
                        nameof(AttackableUnitBase.WeaponFired),
                        new Callable(this, nameof(OnWeaponFired))
                    );

                    attackable.Connect(
                        nameof(AttackableUnitBase.WeaponEmpty),
                        new Callable(this, nameof(OnWeaponEmpty))
                    );

                    attackable.Connect(
                        nameof(AttackableUnitBase.ReloadStarted),
                        new Callable(this, nameof(OnReloadStarted))
                    );

                    attackable.Connect(
                        nameof(AttackableUnitBase.ReloadCompleted),
                        new Callable(this, nameof(OnReloadCompleted))
                    );
                }

                var Fsm = unitNode.GetNodeOrNull<UnitFSM>("UnitFSM");

                if (Fsm != null)
                {
                    Fsm.Connect(
                        nameof(AttackableUnitFSM.TargetCaptured),
                        new Callable(this, nameof(OnTargetCaptured))
                );
                }
            }
        }
    }

    private void VisualiseEventBus()
    {
        foreach(var ev in _eventBus)
        {
            GD.Print($"Event: {ev.Type}, Source: {ev.Source}, Target: {ev.Target}, Amount: {ev.Amount}, Timestamp: {ev.Timestamp}");
        }
    }

    public override void _Process(double delta)
    {
        _gameTime += (float)delta;

        // Purge expired events from the front of the bus
        // The point is that when the AI make the decision, it can query the bus for recent events
        while (_eventBus.Count > 0 && (_gameTime - _eventBus[0].Timestamp) > EventTTL)
        {
            _eventBus.RemoveAt(0);
        }
    }

    private void ConnectPerceptionSignals(PerceptionSystem perc)
    {
        perc.Connect(nameof(PerceptionSystem.VisionUpdate),
            new Callable(this, nameof(VisionUpdate)));
        perc.Connect(nameof(PerceptionSystem.UnitPositionChanged),
            new Callable(this, nameof(OnUnitPositionChanged)));
        perc.Connect(nameof(PerceptionSystem.EnemyDetected),
            new Callable(this, nameof(OnEnemyDetected)));
        perc.Connect(nameof(PerceptionSystem.EnemyLostFocused),
            new Callable(this, nameof(OnEnemyLostFocused)));
        perc.Connect(nameof(PerceptionSystem.CampDetected),
            new Callable(this, nameof(OnCampDetected)));
        perc.Connect(nameof(PerceptionSystem.CampLostFocused),
            new Callable(this, nameof(OnCampLostFocused)));
        perc.Connect(nameof(PerceptionSystem.ObstacleDetected),
            new Callable(this, nameof(ObstacleDetected)));
        perc.Connect(nameof(PerceptionSystem.ObstacleLostFocused),
            new Callable(this, nameof(ObstacleLostFocused)));
    }

    public void VisionUpdate(Vector2[] cells)
    {
        //  Update those cell vision in VisionMap & Update TerrainMap if it hasn't been seen before
        foreach (var cell in cells)
        {
            var temp = (Vector2I)cell;
            _visionMap.Update(temp);

            // Query the map to check for the terrain type
            var map = GetParent().GetParent().GetNode<Map>("Map");
            _terrainMap.Update(temp, map.GetTerrainType(cell * 16));

            _locationMap.UpdateUnseenEnemies(temp);
        }

        // Update the InfluenceMap confidence
        _influenceMap.UpdateConfidence();
    }

    public void OnUnitPositionChanged(UnitBase unit)
    {
        // Update the unit's position in the LocationMap
        _locationMap.UpdateFocused(unit, LocationMap.EntityType.AllyUnit);

        // Update the InfluenceMap grid and confidence
        _influenceMap.UpdateGrid();
        _influenceMap.UpdateConfidence();

        if(_team.IsDebugging) GD.Print("Unit position changed: " + unit.Name + " at " + unit.Position);
    }

    public void OnEnemyDetected(UnitBase enemy)
    {
        // Add enemy to LocationMap
        _locationMap.UpdateFocused(enemy, LocationMap.EntityType.EnemyUnit);

        // InfluenceMap may need an update
        _influenceMap.UpdateGrid();
        _influenceMap.UpdateConfidence();

        // Emit signal for enemy detection (for research purposes)
        EmitSignal(nameof(EnemyDetected));

        if(_team.IsDebugging) GD.Print("Enemy detected: " + enemy.Name + " at " + enemy.Position);
    }

    public void OnEnemyLostFocused(UnitBase enemy)
    {
        // Add enemy to LocationMap
        _locationMap.UpdateNotFocused(enemy);

        // InfluenceMap may need an update
        _influenceMap.UpdateGrid();
        _influenceMap.UpdateConfidence();

        if(_team.IsDebugging) GD.Print("Enemy lost focus: " + enemy.Name + " at " + enemy.Position);
    }

    public void OnCampDetected(CampBase camp)
    {
        // Add camp to LocationMap
        if (camp.TeamId == CampBase.CampOwner.Neutral)
        {
            _locationMap.UpdateFocused(camp, LocationMap.EntityType.NeutralCamp);
        }
        else if ((int)camp.TeamId == TeamId)
        {
            _locationMap.UpdateFocused(camp, LocationMap.EntityType.AllyCamp);
        }
        else
        {
            _locationMap.UpdateFocused(camp, LocationMap.EntityType.EnemyCamp);
        }

        if(_team.IsDebugging) GD.Print("Camp detected: " + camp.Name + " at " + camp.Position);
    }

    public void OnCampLostFocused(CampBase camp)
    {
        // Remove camp (or mark as “seen but not currently visible”)
        _locationMap.UpdateNotFocused(camp);

        if(_team.IsDebugging) GD.Print("Camp lost focus: " + camp.Name + " at " + camp.Position);
    }

    public void ObstacleDetected(Node2D obstacle)
    {
        // Add obstacle to LocationMap
        _locationMap.UpdateFocused(obstacle, LocationMap.EntityType.Obstacle);

        if(_team.IsDebugging) GD.Print("Obstacle detected: " + obstacle.Name + " at " + obstacle.Position);
    }

    public void ObstacleLostFocused(Node2D obstacle)
    {
        // Remove obstacle from LocationMap
        _locationMap.UpdateNotFocused(obstacle);

        if(_team.IsDebugging) GD.Print("Obstacle lost focus: " + obstacle.Name + " at " + obstacle.Position);
    }

    // ------------ Ephemeral events bus methods ------------

    public void OnUnitDamaged(float damage, Node2D source, Node2D target)
    {
        // Enqueue a “damage” event. source is the unit that took damage.
        _eventBus.Add(new TacticalEvent(_gameTime, TacticalEventType.UnitDamaged, source.Name, target.Name, damage));
        // if(_team.IsDebugging) VisualiseEventBus();
    }

    public void OnUnitHealed(float amount, Node2D source, Node2D target)
    {
        // Enqueue a “healed” event.
        _eventBus.Add(new TacticalEvent(_gameTime, TacticalEventType.UnitHealed, source.Name, target.Name, amount));
        // if(_team.IsDebugging) VisualiseEventBus();
    }

    public void OnUnitDestroyed(Node2D source, Vector2I position)
    {
        _locationMap.UpdateAllyDead((UnitBase)source, position);
        // Enqueue a “dead” event.
        _eventBus.Add(new TacticalEvent(_gameTime, TacticalEventType.UnitDestroyed, source.Name, null, 0));
        // if(_team.IsDebugging) VisualiseEventBus();
    }

    public void OnWeaponFired(Node2D source, Node2D target, int remainingAmmo)
    {
        _eventBus.Add(new TacticalEvent(_gameTime, TacticalEventType.WeaponFired, source.Name, target.Name, remainingAmmo));

        // Emit signal for weapon fired (for research purposes)
        EmitSignal(nameof(WeaponFired));
        // if(_team.IsDebugging) VisualiseEventBus();
    }

    public void OnWeaponEmpty(Node2D source)
    {
        _eventBus.Add(new TacticalEvent(_gameTime, TacticalEventType.WeaponEmpty, source.Name));
        // if(_team.IsDebugging) VisualiseEventBus();
    }

    public void OnReloadStarted(Node2D source)
    {
        _eventBus.Add(new TacticalEvent(_gameTime, TacticalEventType.ReloadStarted, source.Name));
        // if(_team.IsDebugging) VisualiseEventBus();
    }

    public void OnReloadCompleted(Node2D source)
    {
        _eventBus.Add(new TacticalEvent(_gameTime, TacticalEventType.ReloadCompleted, source.Name));
        // if(_team.IsDebugging) VisualiseEventBus();
    }

    public void OnTargetCaptured(Node2D source, Node2D target)
    {
        _eventBus.Add(new TacticalEvent(_gameTime, TacticalEventType.TargetCaptured, source.Name, target.Name));
        _locationMap.UpdateFocused((CampBase)target, LocationMap.EntityType.AllyCamp);
        // if(_team.IsDebugging) VisualiseEventBus();
    }
}