using System.Collections.Generic;
using System.Linq;
using Godot;
using NeuroWarCommander.Scripts.AIs.BehaviorTree.Base;
using NeuroWarCommander.Scripts.AIs.BehaviorTree.Tasks.Condition;
using NeuroWarCommander.Scripts.Team.Blackboard;
using NeuroWarCommander.Scripts.Units.Base;

namespace NeuroWarCommander.Scripts.AIs.BehaviorTree.Tasks.Action;

/// Send all available Scout units to scout different locations
public class ActionOrderScout(AIContext context, CheckScoutNeed opportunity, int maxScoutsToUse = 3) : BTNode
{
    public override NodeStatus Tick(double delta)
    {
        // Start with the primary target location
        var gridPos = opportunity.ChosenGrid;

        // Find all available scouts
        var scouts = context.locationMap.GetAllUnits()
            .Where(e => e.Item2.Type == LocationMap.EntityType.AllyUnit)
            .Select(u => u.Item2.EntityNode)
            .OfType<UnitBase>()
            .Where(s => s.CurrentHealth > s.MaxHealth / 4 && s.IsInGroup("scouts"))
            .ToList();

        if (scouts.Count == 0)
            return NodeStatus.Failure;

        // Generate additional scout locations around the primary target
        var scoutLocations = GenerateScoutLocations(gridPos, Mathf.Min(scouts.Count, maxScoutsToUse));

        // Assign scouts to locations based on proximity
        int scoutsSent = 0;
        foreach (var location in scoutLocations)
        {
            if (scoutsSent >= scouts.Count || scoutsSent >= maxScoutsToUse)
                break;

            // Convert grid position to world position
            Vector2I worldTarget = new Vector2I(
                location.X * context.visionMap.CellSize,
                location.Y * context.visionMap.CellSize
            );

            // Find closest scout not already assigned
            var availableScouts = scouts
                .Where(s => s.GetNode<UnitFSM>("UnitFSM").GetCurrentState() == UnitFSM.State.Idle)
                .OrderBy(s => s.GlobalPosition.DistanceTo(worldTarget))
                .ToList();

            if (availableScouts.Count > 0)
            {
                var scout = availableScouts.First();
                Vector2I safeDest = BehaviorTreeUtils.AvoidCollision(
                    worldTarget,
                    context.locationMap,
                    context.visionMap
                );
                scout.GetNode<UnitFSM>("UnitFSM").MoveTo(safeDest, UnitFSM.State.Moving);
                scoutsSent++;
            }
        }

        return scoutsSent > 0 ? NodeStatus.Success : NodeStatus.Failure;
    }

    private List<Vector2I> GenerateScoutLocations(Vector2I primaryTarget, int count)
    {
        var locations = new List<Vector2I> { primaryTarget };

        int radius = 2;

        for (int i = 1; i < count; i++)
        {
            // Calculate position in a circular pattern
            float angle = i * (Mathf.Pi * 2 / count);
            Vector2I offset = new Vector2I(
                Mathf.RoundToInt(Mathf.Cos(angle) * radius),
                Mathf.RoundToInt(Mathf.Sin(angle) * radius)
            );

            Vector2I newLocation = primaryTarget + offset;

            // Validate the location is within bounds
            if (newLocation.X > -(context.visionMap.MapSize.X / 2 / context.visionMap.CellSize) && newLocation.Y >  (context.visionMap.MapSize.Y / 2 / context.visionMap.CellSize) &&
                newLocation.X < (context.visionMap.MapSize.X / 2 / context.visionMap.CellSize) && newLocation.Y < (context.visionMap.MapSize.Y / 2 / context.visionMap.CellSize))
            {
                locations.Add(newLocation);
            }
        }

        return locations;
    }
}