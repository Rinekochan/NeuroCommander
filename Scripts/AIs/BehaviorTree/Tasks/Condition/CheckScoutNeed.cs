using System.Collections.Generic;
using System.Linq;
using Godot;
using NeuroWarCommander.Scripts.AIs.BehaviorTree.Base;
using NeuroWarCommander.Scripts.Camps;
using NeuroWarCommander.Scripts.Team.Blackboard;
using NeuroWarCommander.Scripts.Units.Base;

namespace NeuroWarCommander.Scripts.AIs.BehaviorTree.Tasks.Condition;

// Is there any VisionMap cell older than ScoutAgeThreshold or not seen before
// If yes, store the chosen cell in context for ActionOrderScout.

public class CheckScoutNeed(AIContext context) : BTNode
{
    public Vector2I ChosenGrid { get; private set; }

    public override NodeStatus Tick(double delta)
    {
        var candidates = new List<Vector2I>();

        // VisionMap cells older than threshold
        foreach (var kvp in context.visionMap.VisionCells)
        {
            if (!context.map.GetGrid().GetCell(kvp.Key).Walkable) continue; // Skip non-walkable cells
            if (kvp.Value is >= float.MaxValue - 1.0f or >= BehaviorTreeConstants.ScoutAgeThreshold)
            {
                candidates.Add(kvp.Key);
            }
        }

        var healthyScouts = context.locationMap.GetAllUnits()
            .Select(u => u.Item2?.EntityNode)
            .OfType<UnitBase>()
            .Where(s => s.CurrentHealth > s.MaxHealth * 0.25f && s.IsInGroup("scouts"))
            .ToList();

        // Find the positions of scouts
        var scoutPositions = healthyScouts
            .Select(s => s.GlobalPosition)
            .ToHashSet();

        var averageScoutPosition = scoutPositions.Count > 0
            ? new Vector2I((int)scoutPositions.Average(pos => pos.X), (int)scoutPositions.Average(pos => pos.Y))
            : Vector2I.Zero;

        var unexploredRegions = candidates
            .Where(pos => pos.DistanceTo(averageScoutPosition / context.visionMap.CellSize) < 64) // Not too far
            .Where(pos => pos.DistanceTo(averageScoutPosition / context.visionMap.CellSize) > 15) // Not too close
            .OrderByDescending(pos => context.visionMap.GetVisionTimeOfCell(pos))
            .ThenBy(pos => pos.DistanceTo(averageScoutPosition / context.visionMap.CellSize)) // Prioritize cells closer to scouts
            .ToList();

        if (candidates.Count == 0)
            return NodeStatus.Failure;

        var filteredRegions = unexploredRegions
            .Where(pos =>
            {
                // Check nearby cells for entities
                bool isOccupied = false;
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        Vector2I checkPos = pos + new Vector2I(dx, dy);
                        var cell = context.locationMap.GetLocationCell(checkPos);
                        if (cell?.EntityNode != null)
                        {
                            isOccupied = true;
                            break;
                        }
                    }
                    if (isOccupied) break;
                }
                return !isOccupied;
            })
            .ToList();

        if (filteredRegions.Count == 0 && unexploredRegions.Count > 0)
        {
            ChosenGrid = unexploredRegions.First();
            return NodeStatus.Success;
        }

        ChosenGrid = filteredRegions.Count > 0 ? filteredRegions[0] : Vector2I.Zero; // Just get to the center if no regions found
        return NodeStatus.Success;
    }
}