using System.Collections.Generic;
using System.Linq;
using Godot;
using NeuroWarCommander.Scripts.AIs.BDI.Base;
using NeuroWarCommander.Scripts.Units.Base;

namespace NeuroWarCommander.Scripts.AIs.BDI.Plans;

// Plan to send scouts to explore unexplored regions
public class ScoutingPlan(BDIBeliefBase beliefs) : BDIPlan(beliefs)
{
    private List<UnitBase> _assignedUnits = [];

    public override ExecutionStatus Execute(double delta, List<Vector2I> assignedPositions)
    {
        // Get available scouts
        var scouts = _beliefs.HealthyScouts
            .Where(s => s.GetNode<UnitFSM>("UnitFSM").GetCurrentState() == UnitFSM.State.Idle)
            .ToList();

        if (scouts.Count == 0)
            return ExecutionStatus.Failure;

        var regions = GenerateScoutLocations(_beliefs.BestScoutTarget, scouts.Count);

        for (int i = 0; i < Mathf.Min(scouts.Count, regions.Count); i++)
        {
            var region = regions[i];
            var scout = scouts[i];

            Vector2 targetPos = new Vector2(
                region.X * _beliefs.LocationMap.CellSize,
                region.Y * _beliefs.LocationMap.CellSize
            );

            // Ensure this position isn't already assigned
            if (!assignedPositions.Contains(region))
            {
                var fsm = scout.GetNode<UnitFSM>("UnitFSM");
                fsm.MoveTo(targetPos, UnitFSM.State.Moving);
                assignedPositions.Add(region);
                _assignedUnits.Add(scout);
            }
        }

        return _assignedUnits.Count > 0 ? ExecutionStatus.Running : ExecutionStatus.Failure;
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
            if (newLocation.X > -(beliefs.VisionMap.MapSize.X / 2 / beliefs.VisionMap.CellSize) &&
                newLocation.Y > (beliefs.VisionMap.MapSize.Y / 2 / beliefs.VisionMap.CellSize) &&
                newLocation.X < (beliefs.VisionMap.MapSize.X / 2 / beliefs.VisionMap.CellSize) &&
                newLocation.Y < (beliefs.VisionMap.MapSize.Y / 2 / beliefs.VisionMap.CellSize))
            {
                locations.Add(newLocation);
            }
        }

        return locations;
    }

    public override void Cleanup()
    {
        _assignedUnits.Clear();
    }
}