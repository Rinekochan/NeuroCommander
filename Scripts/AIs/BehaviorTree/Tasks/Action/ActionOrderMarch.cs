using System.Collections.Generic;
using System.Linq;
using Godot;
using NeuroWarCommander.Scripts.AIs.BehaviorTree.Base;
using NeuroWarCommander.Scripts.AIs.BehaviorTree.Tasks.Condition;
using NeuroWarCommander.Scripts.Units.Base;
using NeuroWarCommander.Scripts.Units.Base.AttackableBase;

namespace NeuroWarCommander.Scripts.AIs.BehaviorTree.Tasks.Action;

/// Orders units to march toward an influence hotspot
public class ActionOrderMarch(AIContext context, CheckMarchOpportunity opportunity, int maxUnitsToSend = 3) : BTNode
{
    private readonly List<UnitBase> _assignedUnits = [];
    private readonly List<Vector2I> _newlyAssignedPositions = [];
    private float _taskDuration = 0;
    private const float _taskTimeout = 3.0f;

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
                var fsm = unit.GetNode<UnitFSM>("UnitFSM");
                if (unit.IsQueuedForDeletion() || fsm.GetCurrentState() == UnitFSM.State.Idle)
                {
                    _assignedUnits.Remove(unit);
                    continue;
                }

                if (fsm.GetCurrentState() != UnitFSM.State.Idle)
                {
                    allCompleted = false;
                    break;
                }
            }

            if (_taskDuration > _taskTimeout || allCompleted)
            {
                foreach (var pos in _newlyAssignedPositions)
                {
                    context.assignedPositon.Remove(pos);
                }
                _assignedUnits.Clear();
                _taskDuration = 0;
                return NodeStatus.Success;
            }

            return NodeStatus.Running;
        }


        var gridPos = opportunity.ChosenHotspotGrid;
        Vector2 worldTarget = new Vector2(
            gridPos.X * context.influenceMap.CellSize,
            gridPos.Y * context.influenceMap.CellSize
        );

        // Find all healthy, armed, and not attacking attackers
        var attackers = context.teamNode
            .GetNode<Node>("Units")
            .GetChildren()
            .OfType<AttackableUnitBase>()
            .Where(a => a.CurrentHealth > a.MaxHealth / 3 && !a.IsWeaponReloading())
            .Where(a => a.GetNode<UnitFSM>("UnitFSM").GetCurrentState() == UnitFSM.State.Idle)
            .OrderBy(a => a.GlobalPosition.DistanceTo(worldTarget))
            .Take(maxUnitsToSend)
            .OfType<UnitBase>()
            .ToList();

        // Get commander also
        attackers.Add(context.teamNode
            .GetNode<Node>("Units")
            .GetChildren()
            .OfType<UnitBase>()
            .FirstOrDefault(a => a.IsInGroup("commanders"))
        );

        if (attackers.Count == 0)
            return NodeStatus.Failure;

        int marchersDeployed = 0;

        foreach (var attacker in attackers)
        {
            var fsm = attacker.GetNode<UnitFSM>("UnitFSM");

            // Skip units that are already attacking or moving
            if (fsm.GetCurrentState() != UnitFSM.State.Idle)
                continue;

            // Add small random offsets to prevent crowding
            Vector2I target = Vector2I.Zero;
            bool foundValidPosition = false;
            for (int attempt = 0; attempt < 5; attempt++)
            {
                float offsetX = GD.RandRange(-40, 40);
                float offsetY = GD.RandRange(-40, 40);;
                target = (Vector2I)worldTarget + new Vector2I((int)offsetX, (int)offsetY);

                Vector2I safeDest = BehaviorTreeUtils.AvoidCollision(
                    target,
                    context.locationMap,
                    context.visionMap
                );

                // Check if this position is already assigned
                if (!context.assignedPositon.Contains(safeDest) &&
                    !_newlyAssignedPositions.Contains(safeDest))
                {
                    foundValidPosition = true;
                    _newlyAssignedPositions.Add(safeDest);
                    break;
                }
            }

            if (!foundValidPosition) continue;

            fsm.MoveTo(target, UnitFSM.State.Moving);
            _assignedUnits.Add(attacker);
            marchersDeployed++;

        }

        foreach (var pos in _newlyAssignedPositions)
        {
            context.assignedPositon.Add(pos);
        }

        return marchersDeployed > 0 ? NodeStatus.Success : NodeStatus.Failure;
    }
}