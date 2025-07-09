using Godot;
using System;
using System.Collections.Generic;
using NeuroWarCommander.Scripts.Units.Base;
using NeuroWarCommander.Scripts.Units.Base.AttackableBase;
using NeuroWarCommander.Scripts.Utils;

namespace NeuroWarCommander.Scripts.Test;

public partial class AgentTestController : Node2D
{
    [Export] public NodePath MapPath;
    [Export] public NodePath AgentPath = "Agent"; // Default path to find the agent unit

    [Export] private bool _showDebugPath = true;

    private AttackableUnitBase _agent;
    private AttackableUnitFSM _agentFSM;
    private PathfindingSystem _pathfinding;
    private SteeringSystem _steering;
    private Map _map;

    public override void _Ready()
    {
        CallDeferred(nameof(Initialize));
    }

    public void Initialize(){
        _map = GetNode<Map>(MapPath);
        if (_map == null)
        {
            // Try to find Map in the scene if not specified
            _map = GetTree().Root.GetNode<Map>("World/Map") ??
                  GetTree().Root.GetNode<Map>("Map");

            if (_map == null)
            {
                GD.PrintErr("AgentTestController: Map reference not found!");
            }
            else
            {
                GD.Print("AgentTestController: Map found automatically.");
            }
        }

        // Find the agent unit
        _agent = GetNode<AttackableUnitBase>(AgentPath);

        if (_agent == null)
        {
            GD.PrintErr("AgentTestController: Must have an AttackableUnitBase as a child!");
            return;
        }

        // Get required components
        _agentFSM = _agent.GetNode<AttackableUnitFSM>("UnitFSM");
        _pathfinding = _agent.GetNode<PathfindingSystem>("Pathfinding");
        _steering = _agent.GetNode<SteeringSystem>("Steering");

        // Check if all required components are present
        if (_agentFSM == null)
        {
            GD.PrintErr("AgentTestController: Agent doesn't have an AttackableUnitFSM component!");
        }

        if (_pathfinding == null)
        {
            GD.PrintErr("AgentTestController: Agent doesn't have a PathfindingSystem component!");
        }

        if (_steering == null)
        {
            GD.PrintErr("AgentTestController: Agent doesn't have a SteeringSystem component!");
        }

        // Set up the unit with map reference
        if (_map != null)
        {
            _agent.SetMap(_map);
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (_agent == null || _agentFSM == null) return;

        if (@event is InputEventMouseButton mouseButton && mouseButton.Pressed)
        {
            Vector2 clickPosition = GetGlobalMousePosition();

            // Right click to move
            if (mouseButton.ButtonIndex == MouseButton.Right)
            {
                if (_map != null && _pathfinding != null)
                {
                    _agentFSM.MoveTo(clickPosition, UnitFSM.State.Moving);
                }
            }
            // Left click to attack
            else if (mouseButton.ButtonIndex == MouseButton.Left)
            {
                // Physics query to find target at mouse position
                var spaceState = GetWorld2D().DirectSpaceState;
                var query = new PhysicsPointQueryParameters2D
                {
                    Position = clickPosition,
                    CollideWithBodies = true,
                    CollisionMask = 0xFFFFFFFF // All layers
                };

                var results = spaceState.IntersectPoint(query);

                if (results.Count > 0)
                {
                    // Find the first valid target
                    foreach (var result in results)
                    {
                        var clickedObject = result["collider"].As<Node2D>();
                        if (clickedObject != null && clickedObject != _agent)
                        {
                            // Check if clicked object is an attackable target
                            if (clickedObject is UnitBase ||
                                clickedObject.GetParent() is UnitBase ||
                                clickedObject.HasMethod("TakeDamage"))
                            {
                                _agentFSM.AttackTarget(clickedObject);
                                return;
                            }
                        }
                    }
                }
            }
        }

        if (@event is InputEventKey eventKey && eventKey.Pressed)
        {
            if (eventKey.Keycode == Key.Space)
            {
                // Space to toggle stop/idle
                _agentFSM.TransitionToState((int)AttackableUnitFSM.State.Idle);
                if (_steering != null)
                {
                    _steering.Stop();
                }
            }
        }
    }
}
