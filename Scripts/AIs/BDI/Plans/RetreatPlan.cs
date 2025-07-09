using System.Collections.Generic;
using System.Linq;
using Godot;
using NeuroWarCommander.Scripts.AIs.BDI.Base;
using NeuroWarCommander.Scripts.Units.Base;

namespace NeuroWarCommander.Scripts.AIs.BDI.Plans;

// Plan to retreat low health units to safe locations
public class RetreatPlan(BDIBeliefBase beliefs) : BDIPlan(beliefs)
{
    private List<UnitBase> _assignedUnits = [];

    public override ExecutionStatus Execute(double delta, List<Vector2I> assignedPositions)
    {
        // Find all low health units
        var lowHealthUnits = _beliefs.TeamNode
            .GetNode<Node>("Units")
            .GetChildren()
            .OfType<UnitBase>()
            .Where(u => u.CurrentHealth < u.MaxHealth * 0.3f)
            .ToList();

        if (lowHealthUnits.Count == 0)
            return ExecutionStatus.Success;

        // Find friendly bases or safe areas to retreat to
        Vector2 retreatPosition;

        var friendlyCamps = _beliefs.LocationMap.GetAllAllyCamps().ToList();
        if (friendlyCamps.Count > 0)
        {
            // Retreat to any friendly camp
            retreatPosition = new Vector2(
                friendlyCamps[0].Item1.X * _beliefs.LocationMap.CellSize,
                friendlyCamps[0].Item1.Y * _beliefs.LocationMap.CellSize
            );
        }
        else
        {
            // If no friendly camps, retreat to a corner of the map
            retreatPosition = Vector2.Zero;
        }

        int retreatedUnits = 0;

        foreach (var unit in lowHealthUnits)
        {
            var fsm = unit.GetNode<UnitFSM>("UnitFSM");

            // Skip units that are already moving
            if (fsm.GetCurrentState() != UnitFSM.State.Idle)
                continue;

            // Add small random offset to prevent crowding
            float offsetX = GD.RandRange(-40, 40);
            float offsetY = GD.RandRange(-40, 40);
            Vector2 finalRetreatPos = retreatPosition + new Vector2(offsetX, offsetY);

            // Convert to grid position for checking
            Vector2I gridPosition = new Vector2I(
                Mathf.RoundToInt(finalRetreatPos.X / _beliefs.LocationMap.CellSize),
                Mathf.RoundToInt(finalRetreatPos.Y / _beliefs.LocationMap.CellSize)
            );

            // Check if position is already assigned
            if (!assignedPositions.Contains(gridPosition))
            {
                fsm.MoveTo(finalRetreatPos, UnitFSM.State.Moving);
                assignedPositions.Add(gridPosition);
                _assignedUnits.Add(unit);
                retreatedUnits++;
            }
        }

        return retreatedUnits > 0 ? ExecutionStatus.Running : ExecutionStatus.Success;
    }

    public override void Cleanup()
    {
        _assignedUnits.Clear();
    }
}