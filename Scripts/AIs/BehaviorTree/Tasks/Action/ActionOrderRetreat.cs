using System.Collections.Generic;
using System.Linq;
using Godot;
using NeuroWarCommander.Scripts.AIs.BehaviorTree.Base;
using NeuroWarCommander.Scripts.Camps;
using NeuroWarCommander.Scripts.Units.Base;

namespace NeuroWarCommander.Scripts.AIs.BehaviorTree.Tasks.Action;

public class ActionOrderRetreat(AIContext context) : BTNode
{
    private const float LowHealthFrac = 0.30f;

    public override NodeStatus Tick(double delta)
    {
        var allUnits = context.teamNode
            .GetNode<Node>("Units")
            .GetChildren()
            .OfType<UnitBase>()
            .Where(u => u.CurrentHealth / u.MaxHealth <= LowHealthFrac && u.CurrentHealth > 0)
            .ToList();

        // Get allied camps
        var alliedCamps = context.locationMap.GetAllAllyCamps()
            .Select(t => t.Item2.EntityNode as CampBase)
            .ToList();

        foreach (var unit in allUnits)
        {
            var fsm = unit.GetNode<UnitFSM>("UnitFSM");
            // If unit is already retreating/moving, skip
            if (fsm.GetCurrentState() == UnitFSM.State.Moving)
            {
                continue;
            }

            // If we have allied camps, send to nearest
            if (alliedCamps.Count > 0)
            {
                var nearestCamp = alliedCamps
                    .OrderBy(c => c.GlobalPosition.DistanceTo(unit.GlobalPosition))
                    .First();
                fsm.MoveTo(nearestCamp.GlobalPosition, UnitFSM.State.Moving);
            }
            else
            {
                // Flee from nearest visible enemy
                var visibleEnemies = unit
                    .GetNode<PerceptionSystem>("VisionCircle")
                    .GetVisibleEnemies()
                    .OfType<UnitBase>()
                    .ToList();

                if (visibleEnemies.Count > 0)
                {
                    var nearestEnemy = visibleEnemies
                        .OrderBy(e => e.GlobalPosition.DistanceTo(unit.GlobalPosition))
                        .First();
                    Vector2 fleeDir = (unit.GlobalPosition - nearestEnemy.GlobalPosition).Normalized() * 200f;
                    Vector2 fleeTarget = unit.GlobalPosition + fleeDir;
                    fsm.MoveTo(fleeTarget, UnitFSM.State.Moving);
                }
            }
        }

        return NodeStatus.Running;
    }
}