using Godot;
using System;
using System.Collections.Generic;
using NeuroWarCommander.Scripts.Units.Base;
using NeuroWarCommander.Scripts.Units.Base.AttackableBase;
using NeuroWarCommander.Scripts.Utils.GlobalOverlay;
using NeuroWarCommander.Scripts.Utils.TeamOverlay;

namespace NeuroWarCommander.Scripts.Team;

public partial class Team : Node2D
{
    public enum AIType { BehaviorTree, BDI }

    [Export] public int TeamId { get; set; } = 1; // Default to team 1 (blue)

    [Export] public int RiflemanCount { get; set; } = 0;
    [Export] public int SniperCount { get; set; } = 0;
    [Export] public int TankerCount { get; set; } = 0;
    [Export] public int ScoutCount { get; set; } = 0;
    [Export] public int SiegeMachineCount { get; set; } = 0;
    [Export] public int MedicCount { get; set; } = 0;

    [Export] public AIType ChosenAI { get; set; } = AIType.BehaviorTree;

    public bool IsDebugging = false;

    // References to necessary nodes
    private Blackboard.Blackboard _blackboard;
    private Node _unitsNode;
    private Node _mapNode;
    private TacticalMapOverlay _tacticalMapOverlay;
    private OverlayMode _lastOverlayMode;

    // Unit scene paths
    private readonly Dictionary<string, string> _unitScenePaths = new();

    public override void _Ready()
    {
        // Get references to required nodes
        _unitsNode = GetNode("Units");
        _mapNode = GetParent().GetNode("Map");

        // Initialize unit scene paths based on team ID
        InitializeUnitPaths();

        // Spawn all units
        SpawnUnits();

        // Set team ID for the blackboard
        _blackboard = GetNode<Blackboard.Blackboard>("Blackboard");
        _blackboard.TeamId = TeamId;

        _tacticalMapOverlay = GetNode<TacticalMapOverlay>("TacticalMapOverlay");

        // We will set visualisation settings based on the OverlayMode
        CallDeferred(nameof(SetOverlayVisualisationSettings));

        // Set team ID for the map overlay
        var influenceOverlay = _tacticalMapOverlay.GetNode<InfluenceOverlay>("InfluenceOverlay");
        influenceOverlay.TeamId = TeamId;

        // Instantiate the chosen AI controller under this Team node
        InstantiateAIController();
    }

    private void InitializeUnitPaths()
    {
        string teamColor = TeamId == 1 ? "blue" : "red";

        _unitScenePaths["Commander"] = $"res://Scenes/Units/Commander/{teamColor}_commander.tscn";
        _unitScenePaths["Rifleman"] = $"res://Scenes/Units/Rifleman/{teamColor}_rifleman.tscn";
        _unitScenePaths["Sniper"] = $"res://Scenes/Units/Sniper/{teamColor}_sniper.tscn";
        _unitScenePaths["Tanker"] = $"res://Scenes/Units/Tanker/{teamColor}_tanker.tscn";
        _unitScenePaths["Scout"] = $"res://Scenes/Units/Scout/{teamColor}_scout.tscn";
        _unitScenePaths["SiegeMachine"] = $"res://Scenes/Units/SiegeMachine/{teamColor}_siege_machine.tscn";
        _unitScenePaths["Medic"] = $"res://Scenes/Units/Medic/{teamColor}_medic.tscn";
    }

    private void InstantiateAIController()
    {
        string path = ChosenAI switch
        {
            AIType.BehaviorTree => "res://Scenes/Team/AI/BehaviorTreeAI.tscn",
            AIType.BDI          => "res://Scenes/Team/AI/BDIControllerAI.tscn",
            _ => ""
        };

        if (path == "")
            return;

        var packed = ResourceLoader.Load<PackedScene>(path);
        if (packed == null)
        {
            GD.PrintErr($"[Team] Failed to load AI scene at {path}");
            return;
        }

        var aiInstance = packed.Instantiate<Node2D>();
        AddChild(aiInstance);
    }

    public override void _Process(double delta)
    {
        // We only need to update the overlay visualisation settings if the overlay mode has changed
        if (_lastOverlayMode != _tacticalMapOverlay.OverlayMode)
        {
            SetOverlayVisualisationSettings();
        }
    }

    public override void _Input(InputEvent @event)
    {
        // We will press D to toggle debug mode
        if (@event is InputEventKey { Pressed: true, Keycode: Key.D })
        {
            IsDebugging = !IsDebugging;
            foreach (Node child in _unitsNode.GetChildren())
            {
                if (child is UnitBase unit)
                {
                    unit.IsDebugging = IsDebugging;
                }
            }
        }
    }

    private void SetOverlayVisualisationSettings()
    {
        var units = _unitsNode.GetChildren();
        // Set visualisation settings based on the current overlay mode
        _lastOverlayMode = _tacticalMapOverlay.OverlayMode;

        foreach (UnitBase unit in units)
        {
            unit.IsDebugging = IsDebugging;
            switch (_tacticalMapOverlay.OverlayMode)
            {
                case OverlayMode.Environment:
                    unit.ShowHealthBar = false;
                    unit.ShowPerceptionSystemVisualisation = true;
                    unit.ShowSteeringSystemVisualisation = false;
                    unit.ShowPathfindingSystemVisualisation = true;
                    break;
                case OverlayMode.Influence:
                    unit.ShowHealthBar = true;
                    unit.ShowPerceptionSystemVisualisation = true;
                    unit.ShowSteeringSystemVisualisation = false;
                    unit.ShowPathfindingSystemVisualisation = false;
                    break;
                case OverlayMode.Movement:
                    unit.ShowHealthBar = false;
                    unit.ShowPerceptionSystemVisualisation = false;
                    unit.ShowSteeringSystemVisualisation = true;
                    unit.ShowPathfindingSystemVisualisation = true;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (unit is AttackableUnitBase attackableUnit)
            {
                attackableUnit.ShowAmmoBar = _tacticalMapOverlay.OverlayMode switch
                {
                    OverlayMode.Environment => false,
                    OverlayMode.Influence => true,
                    OverlayMode.Movement => false,
                    _ => attackableUnit.ShowAmmoBar
                };
            }
        }
    }

    private void SpawnUnits()
    {
        Vector2 basePosition = GlobalPosition;

        // Commander is always spawned (one per team)
        SpawnUnit("Commander", basePosition);

        // Create a list of all units to spawn
        var unitsToSpawn = new List<string>();

        // Add units to the list based on counts
        for (int i = 0; i < RiflemanCount; i++) unitsToSpawn.Add("Rifleman");
        for (int i = 0; i < SniperCount; i++) unitsToSpawn.Add("Sniper");
        for (int i = 0; i < TankerCount; i++) unitsToSpawn.Add("Tanker");
        for (int i = 0; i < ScoutCount; i++) unitsToSpawn.Add("Scout");
        for (int i = 0; i < SiegeMachineCount; i++) unitsToSpawn.Add("SiegeMachine");
        for (int i = 0; i < MedicCount; i++) unitsToSpawn.Add("Medic");

        // Spawn units using a grid-based approach with collision checking
        SpawnUnitsInFormation(unitsToSpawn, basePosition);
    }

    private void SpawnUnitsInFormation(List<string> units, Vector2 basePosition)
    {
        if (units.Count == 0) return;

        // Formation parameters
        float unitSpacing = 80.0f;
        int minRows = 3;
        int maxUnitsPerRow = 5;

        int totalRows = Mathf.Max(minRows, Mathf.CeilToInt((float)units.Count / maxUnitsPerRow));
        int unitsPerRow = Mathf.CeilToInt((float)units.Count / totalRows);

        // Calculate the formation width and height
        float formationWidth = unitsPerRow * unitSpacing;
        float formationHeight = totalRows * unitSpacing;

        // Starting position (top-left of the formation)
        Vector2 startPos;

        // Team 1 faces right, team 2 faces left
        if (TeamId == 1)
        {
            startPos = basePosition + new Vector2(-formationWidth/2, -formationHeight/2 + unitSpacing);
        }
        else
        {
            startPos = basePosition + new Vector2(formationWidth/2, -formationHeight/2 + unitSpacing);
        }

        int unitIndex = 0;

        // Place units in grid formation
        for (int row = 0; row < totalRows && unitIndex < units.Count; row++)
        {
            for (int col = 0; col < unitsPerRow && unitIndex < units.Count; col++)
            {
                string unitType = units[unitIndex];

                // Calculate position with some randomization to avoid perfect grid patterns
                float randOffsetX = GD.Randf() * (unitSpacing * 0.3f) - (unitSpacing * 0.15f);
                float randOffsetY = GD.Randf() * (unitSpacing * 0.3f) - (unitSpacing * 0.15f);

                Vector2 position;
                if (TeamId == 1)
                {
                    position = startPos + new Vector2(
                        col * unitSpacing + randOffsetX,
                        row * unitSpacing + randOffsetY
                    );
                }
                else
                {
                    position = startPos + new Vector2(
                        -col * unitSpacing + randOffsetX,
                        row * unitSpacing + randOffsetY
                    );
                }

                // Check for collisions
                position = FindSafePosition(position, unitType);

                // Spawn the unit
                SpawnUnit(unitType, position);
                unitIndex++;
            }
        }
    }

    private Vector2 FindSafePosition(Vector2 position, string unitType)
    {
        // Create a space state for physics queries
        var spaceState = GetWorld2D().DirectSpaceState;

        float radius = 30.0f; // Default collision size

        if (unitType == "Tanker") radius = 40.0f;
        else if (unitType == "SiegeMachine") radius = 45.0f;

        // Parameters for collision check
        var shape = new CircleShape2D();
        shape.Radius = radius;

        var query = new PhysicsShapeQueryParameters2D();
        query.Shape = shape;
        query.CollisionMask = 1 << 1 | 1 << 2; // Unit and objects collision layer

        // Try up to 10 alternative positions
        int maxAttempts = 10;
        int attempt = 0;

        Vector2 testPosition = position;
        float searchDistance = 20.0f;

        while (attempt < maxAttempts)
        {
            query.Transform = new Transform2D(0, testPosition);
            var results = spaceState.IntersectShape(query);

            // If no collision, this position is safe
            if (results.Count == 0)
                return testPosition;

            // Try a new position with increasing distance
            float angle = (float)attempt / maxAttempts * Mathf.Pi * 2;
            searchDistance += 15.0f;

            testPosition = position + new Vector2(
                Mathf.Cos(angle) * searchDistance,
                Mathf.Sin(angle) * searchDistance
            );

            attempt++;
        }

        // If we couldn't find a safe position, return the original one
        return position;
    }

    private void SpawnUnit(string unitType, Vector2 position)
    {
        if (!_unitScenePaths.ContainsKey(unitType))
        {
            GD.PrintErr($"Unit type {unitType} not found in scene paths");
            return;
        }

        // Load the scene
        PackedScene unitScene = ResourceLoader.Load<PackedScene>(_unitScenePaths[unitType]);
        if (unitScene == null)
        {
            GD.PrintErr($"Failed to load scene: {_unitScenePaths[unitType]}");
            return;
        }

        // Instance the scene
        Node2D unitInstance = (Node2D)unitScene.Instantiate();

        // Set position
        unitInstance.Position = position;

        GD.Print($"Spawned {unitType} for Team {TeamId} at {position}");
        // Set TeamId if it's a UnitBase or derived class
        if (unitInstance is UnitBase unitBase)
        {
            unitBase.TeamId = TeamId;

            // Connect to the map
            unitBase.SetMap(_mapNode);
        }

        if (TeamId == 2) unitInstance.Rotate(MathF.PI);

        // Add to Units node
        _unitsNode.AddChild(unitInstance);

    }
}