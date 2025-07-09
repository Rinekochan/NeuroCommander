using System.Collections.Generic;
using System.Linq;
using Godot;
using NeuroWarCommander.Scripts.AIs.BehaviorTree.Base;
using NeuroWarCommander.Scripts.Team.Blackboard;
using NeuroWarCommander.Scripts.Units.Base;
using NeuroWarCommander.Scripts.Units.Base.AttackableBase;

namespace NeuroWarCommander.Scripts.AIs.BehaviorTree.Tasks.Condition;

public class CheckMarchOpportunity(AIContext context) : BTNode
{
    public Vector2I ChosenHotspotGrid { get; private set; }

    public override NodeStatus Tick(double delta)
    {
        bool isSeeingTheirCommander =
            context.locationMap.GetAllUnits()
            .Select(u => u.Item2)
            .Where(e => e.Type == LocationMap.EntityType.EnemyUnit)
            .Select(e => e.EntityNode)
            .OfType<UnitBase>()
            .Where(GodotObject.IsInstanceValid)
            .Any(unit => unit.IsInGroup("commanders"));

        // If we are seeing their commander, we can march on them directly
        if (isSeeingTheirCommander)
        {
            ChosenHotspotGrid = (Vector2I)context.locationMap.GetAllUnits()
                .Select(u => u.Item2)
                .Where(e => e.Type == LocationMap.EntityType.EnemyUnit)
                .Select(e => e.EntityNode)
                .OfType<UnitBase>()
                .Where(unit => unit.IsInGroup("commanders"))
                .Select(unit => unit.GlobalPosition / context.influenceMap.CellSize)
                .FirstOrDefault();

            // GD.Print($"CheckMarchOpportunity: See their commander at hotspot {ChosenHotspotGrid}");
            return NodeStatus.Success;
        }

        // Find all confident hotspots (TotalInfluence > 0 and Confidence = true)
        var potentialHotspots = context.influenceMap.InfluenceCells
            .Where(kvp => kvp.Value.Confidence)
            .Where(kvp => kvp.Value.TotalInfluence > BehaviorTreeConstants.MinAttackInfluence)
            .Select(kvp => kvp)
            .ToList();

        var hotspots = new List<Vector2I>();

        var bestTarget = potentialHotspots
            .OrderBy(target => target.Value.TotalInfluence)
            .ThenBy(target => target.Value.Confidence)
            .Select(target => target.Key)
            .FirstOrDefault();

        if (hotspots.Count == 0)
            return NodeStatus.Failure;

        ChosenHotspotGrid = bestTarget;

        // GD.Print($"CheckMarchOpportunity: chosen hotspot {ChosenHotspotGrid}");
        return NodeStatus.Success;
    }
}