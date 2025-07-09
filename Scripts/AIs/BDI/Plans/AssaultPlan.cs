using System.Collections.Generic;
using System.Linq;
using Godot;
using NeuroWarCommander.Scripts.AIs.BDI.Base;
using NeuroWarCommander.Scripts.AIs.BehaviorTree;
using NeuroWarCommander.Scripts.Team.Blackboard;
using NeuroWarCommander.Scripts.Units.Base;
using NeuroWarCommander.Scripts.Units.Base.AttackableBase;

namespace NeuroWarCommander.Scripts.AIs.BDI.Plans;

// Plan for coordinated assault on enemy positions
public class AssaultPlan(BDIBeliefBase beliefs) : BDIPlan(beliefs)
{
    private List<UnitBase> _marchingUnits = [];
    private List<UnitBase> _attackingUnits = [];

    public override ExecutionStatus Execute(double delta, List<Vector2I> assignedPositions)
    {
        // First, march units to the target hotspot
        ExecuteMarchPhase(assignedPositions);

        // Then engage any visible enemies
        ExecuteAttackPhase();

        // Consider the plan successful if we've engaged units in either activity
        return (_marchingUnits.Count > 0 || _attackingUnits.Count > 0) ?
            ExecutionStatus.Running : ExecutionStatus.Success;
    }

    private void ExecuteMarchPhase(List<Vector2I> assignedPositions)
    {
        if (_beliefs.BestAttackTarget == Vector2I.Zero) return;

        Vector2 targetWorldPos = new Vector2(
            _beliefs.BestAttackTarget.X * _beliefs.InfluenceMap.CellSize,
            _beliefs.BestAttackTarget.Y * _beliefs.InfluenceMap.CellSize
        );

        // Get available combat units for marching
        var attackers = _beliefs.HealthyAttackers
            .Where(a => a.GetNode<UnitFSM>("UnitFSM").GetCurrentState() == UnitFSM.State.Idle)
            .OrderBy(a => a.GlobalPosition.DistanceTo(targetWorldPos))
            .OfType<UnitBase>()
            .Take(5) // Send 5 units
            .ToList();

        // Add commander if available
        var commander = _beliefs.TeamNode
            .GetNode<Node>("Units")
            .GetChildren()
            .OfType<UnitBase>()
            .FirstOrDefault(u => u.IsInGroup("commanders"));

        if (commander != null && !attackers.Contains(commander))
        {
            attackers.Add(commander);
        }

        if (attackers.Count == 0) return;

        foreach (var attacker in attackers)
        {
            // Skip if this unit is already marching
            if (_marchingUnits.Contains(attacker))
                continue;

            // Add small random offsets to prevent crowding
            float offsetX = GD.RandRange(-40, 40);
            float offsetY = GD.RandRange(-40, 40);
            Vector2 finalPos = targetWorldPos + new Vector2(offsetX, offsetY);

            // Convert to grid position
            Vector2I gridPos = new Vector2I(
                Mathf.RoundToInt(finalPos.X / _beliefs.LocationMap.CellSize),
                Mathf.RoundToInt(finalPos.Y / _beliefs.LocationMap.CellSize)
            );

            // Check if this position is already assigned
            if (!assignedPositions.Contains(gridPos))
            {
                var fsm = attacker.GetNode<UnitFSM>("UnitFSM");
                fsm.MoveTo(finalPos, UnitFSM.State.Moving);
                assignedPositions.Add(gridPos);
                _marchingUnits.Add(attacker);
            }
        }
    }

    private void ExecuteAttackPhase()
    {
        // Find all combat units that can attack
        var attackers = _beliefs.HealthyAttackers
            .Where(a => !_attackingUnits.Contains(a))
            .Where(a => a is AttackableUnitBase)
            .Take(5) // Send 5 units
            .ToList();

        if (attackers.Count == 0) return;

        foreach (var attacker in attackers)
        {
            var attackableUnit = attacker;
            var fsm = attackableUnit.GetNode<AttackableUnitFSM>("UnitFSM");

            // Skip units that are already attacking
            if (fsm.GetAttackTarget() != null)
                continue;

            // Look for valid targets in perception systems
            var attackTargets = FindAttackTargets(attackableUnit);

            if (attackTargets.Count > 0)
            {
                // Attack closest target
                var target = attackTargets
                    .OrderBy(t => attacker.GlobalPosition.DistanceTo(t.GlobalPosition))
                    .First();

                fsm.AttackTarget(target);
                _attackingUnits.Add(attacker);
            }
        }
    }

    private List<Node2D> FindAttackTargets(AttackableUnitBase unit)
    {
        var targets = new List<Node2D>();

        // Check FOV cone first
        var fovCone = unit.GetNode<PerceptionSystem>("FOVCone");
        if (fovCone != null)
        {
            var visibleEnemies = fovCone.GetVisibleEnemies();
            foreach (var enemy in visibleEnemies)
            {
                if (enemy is Node2D node)
                {
                    targets.Add(node);
                }
            }

            var visibleCamps = fovCone.GetVisibleCamps();
            foreach (var camp in visibleCamps)
            {
                if (camp is Node2D node)
                {
                    // Ensure it's not a friendly camp
                    var campScript = node as GodotObject;
                    if ((int)campScript.Get("TeamId") != unit.TeamId)
                    {
                        targets.Add(node);
                    }
                }
            }
        }

        // If we found nothing in FOV cone, try vision circle
        if (targets.Count == 0)
        {
            var visionCircle = unit.GetNode<PerceptionSystem>("VisionCircle");
            if (visionCircle != null)
            {
                var visibleEnemies = visionCircle.GetVisibleEnemies();
                foreach (var enemy in visibleEnemies)
                {
                    if (enemy is Node2D node)
                    {
                        targets.Add(node);
                    }
                }

                var visibleCamps= visionCircle.GetVisibleCamps();
                foreach (var camp in visibleCamps)
                {
                    if (camp is Node2D node)
                    {
                        // Ensure it's not a friendly camp
                        var campScript = node as GodotObject;
                        if ((int)campScript.Get("TeamId") != unit.TeamId)
                        {
                            targets.Add(node);
                        }
                    }
                }
            }
        }

        // If we still found nothing, try the location map
        if (targets.Count == 0)
        {
            // Get nearby enemies from location map
            var enemies = _beliefs.LocationMap.GetAllUnits()
                .Where(e => e.Item2.Type == LocationMap.EntityType.EnemyUnit)
                .Where(e => unit.GlobalPosition.DistanceTo(e.Item2.EntityNode.GlobalPosition) < 250)
                .Select(e => e.Item2.EntityNode)
                .ToList();

            targets.AddRange(enemies);

            // Add enemy camps if close enough
            var enemyCamps = _beliefs.LocationMap.GetAllEnemyCamps()
                .Where(e => unit.GlobalPosition.DistanceTo(e.Item2.EntityNode.GlobalPosition) < 300)
                .Select(e => e.Item2.EntityNode)
                .ToList();

            targets.AddRange(enemyCamps);

            // Add neutral camps if close enough
            var neutralCamps = _beliefs.LocationMap.GetAllNeutralCamps()
                .Where(e => unit.GlobalPosition.DistanceTo(e.Item2.EntityNode.GlobalPosition) < 200)
                .Select(e => e.Item2.EntityNode)
                .ToList();

            targets.AddRange(neutralCamps);
        }

        return targets;
    }

    public override void Cleanup()
    {
        _marchingUnits.Clear();
        _attackingUnits.Clear();
    }
}