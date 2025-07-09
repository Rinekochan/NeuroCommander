using System.Collections.Generic;
using System.Linq;
using Godot;
using NeuroWarCommander.Scripts.AIs.BehaviorTree.Base;
using NeuroWarCommander.Scripts.AIs.BehaviorTree.Tasks.Condition;
using NeuroWarCommander.Scripts.Team.Blackboard;
using NeuroWarCommander.Scripts.Units.Base;
using NeuroWarCommander.Scripts.Units.Base.AttackableBase;

namespace NeuroWarCommander.Scripts.AIs.BehaviorTree.Tasks.Action;

/// Orders units to attack visible targets within their perception range
public class ActionOrderAttack(AIContext context, int maxUnitsToEngage = 5) : BTNode
{
    private readonly List<UnitBase> _assignedUnits = new();
    private float _taskDuration = 0;
    private const float _taskTimeout = 2.0f;

    public override NodeStatus Tick(double delta)
    {
        // Check if we're already running a task
        if (_assignedUnits.Count > 0)
        {
            _taskDuration += (float)delta;

            // Check if task has timed out or if all units have completed their tasks
            bool allCompleted = true;

            foreach (var unit in _assignedUnits.ToList())
            {
                if (!GodotObject.IsInstanceValid(unit) || unit.IsQueuedForDeletion())
                {
                    _assignedUnits.Remove(unit);
                    continue;
                }

                var fsm = unit.GetNode<AttackableUnitFSM>("UnitFSM");
                if (fsm.GetCurrentState() == UnitFSM.State.Idle)
                {
                    _assignedUnits.Remove(unit);
                    continue;
                }

                if (fsm.GetAttackTarget() != null || fsm.GetCurrentState() != UnitFSM.State.Idle)
                {
                    // Still engaged in combat
                    allCompleted = false;
                    break;
                }
            }

            // If timeout or all completed, reset task
            if (_taskDuration > _taskTimeout || allCompleted)
            {
                _assignedUnits.Clear();
                _taskDuration = 0;
                return NodeStatus.Success;
            }

            return NodeStatus.Running;
        }

        // Find all combat units
        var attackers = context.teamNode
            .GetNode<Node>("Units")
            .GetChildren()
            .OfType<AttackableUnitBase>()
            .Where(a => a.CurrentHealth > a.MaxHealth / 3)
            .OrderByDescending(a => a.CurrentHealth / a.MaxHealth) // Prefer healthier units first
            .Take(maxUnitsToEngage)
            .ToList();

        if (attackers.Count == 0)
            return NodeStatus.Failure;

        int engagedUnits = 0;

        // For each attacker, check if they can see any targets
        foreach (var attacker in attackers)
        {
            var fsm = attacker.GetNode<AttackableUnitFSM>("UnitFSM");

            // Get all possible targets from the location map that's near enough
            var possibleTargets = context.locationMap.GetAllNeutralCamps()
                .Select(e => e.Item2.EntityNode)
                .ToList();

            possibleTargets.AddRange(context.locationMap.GetAllEnemyCamps()
                .Select(e => e.Item2.EntityNode)
                .ToList()
            );

            possibleTargets.AddRange(context.locationMap.GetAllUnits()
                .Where(e => e.Item2.Type == LocationMap.EntityType.EnemyUnit)
                .Select(e => e.Item2.EntityNode)
                .ToList()
            );

            if (possibleTargets.Count > 0)
            {
                var target = possibleTargets
                    .OrderBy(e => e.GlobalPosition.DistanceTo(attacker.GlobalPosition))
                    .First();

                fsm.AttackTarget(target);
                _assignedUnits.Add(attacker);
                engagedUnits++;
            }
        }
        return engagedUnits > 0 ? NodeStatus.Success : NodeStatus.Failure;
    }
}