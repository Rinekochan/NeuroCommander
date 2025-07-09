using Godot;
using NeuroWarCommander.Scripts.Team.Blackboard;

namespace NeuroWarCommander.Scripts.AIs.BehaviorTree;

public static class BehaviorTreeConstants
{
    public const float MinAttackInfluence = 1.0f;
    public const float ScoutAgeThreshold = 15.0f;
    public const float ScoutMaxDistance = 800.0f;
}

// Utility methods
public static class BehaviorTreeUtils
{
    // Avoids collision by checking the desired position against the location map and vision map.
    public static Vector2I AvoidCollision(Vector2I desiredWorldPos, LocationMap lmap, VisionMap vmap)
    {
        Vector2I gridPos = desiredWorldPos / vmap.CellSize;
        var cell = lmap.GetLocationCell(gridPos);
        if (cell?.EntityNode == null)
            return desiredWorldPos;

        Vector2I[] dirs = {
            new(1,0), new(-1,0),
            new(0,1), new(0,-1),
            new(1,1), new(1,-1),
            new(-1,1), new(-1,-1)
        };

        foreach (var d in dirs)
        {
            var neighbor = gridPos + d;
            var c = lmap.GetLocationCell(neighbor);
            if (c?.EntityNode == null)
            {
                return new Vector2I(
                    neighbor.X * vmap.CellSize,
                    neighbor.Y * vmap.CellSize
                );
            }
        }
        return desiredWorldPos;
    }
}