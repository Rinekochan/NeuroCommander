using System.Collections.Generic;
using System.Linq;
using Godot;
using NeuroWarCommander.Scripts.AIs.BDI.Base;
using NeuroWarCommander.Scripts.Units.Base;
using NeuroWarCommander.Scripts.Units.Base.AttackableBase;

namespace NeuroWarCommander.Scripts.AIs.BDI.Plans;

// Plan to capture neutral camps and resources
public class CapturePlan(BDIBeliefBase beliefs) : BDIPlan(beliefs)
{
    private List<UnitBase> _assignedUnits = [];

    public override ExecutionStatus Execute(double delta, List<Vector2I> assignedPositions)
    {
        // Find all neutral camps
        var camps = _beliefs.LocationMap.GetAllNeutralCamps().ToList();
        camps.AddRange(_beliefs.LocationMap.GetAllEnemyCamps().ToList());

        if (camps.Count == 0)
            return ExecutionStatus.Success;

        var ourUnits = _beliefs.TeamNode
            .GetNode<Node>("Units")
            .GetChildren()
            .OfType<UnitBase>()
            .ToList();

        if (ourUnits.Count == 0)
            return ExecutionStatus.Failure;

        Vector2 forcesCenter = Vector2.Zero;
        foreach (var unit in ourUnits)
        {
            if (unit.IsInGroup("scouts")) continue; // Skip scouts
            forcesCenter += unit.GlobalPosition;
        }
        forcesCenter /= ourUnits.Count;

        // Sort camps by proximity to our forces
        var sortedCamps = camps
            .OrderBy(c =>
            {
                Vector2 campPos = new Vector2(
                    c.Item1.X * _beliefs.LocationMap.CellSize,
                    c.Item1.Y * _beliefs.LocationMap.CellSize
                );
                return forcesCenter.DistanceTo(campPos);
            })
            .ToList();

        // Get available attackers
        var attackers = _beliefs.HealthyAttackers
            .Where(a => a.GetNode<UnitFSM>("UnitFSM").GetCurrentState() == UnitFSM.State.Idle)
            .ToList();

        if (attackers.Count == 0)
            return ExecutionStatus.Failure;

        int unitsDeployed = 0;
        int maxCamps = Mathf.Min(sortedCamps.Count, attackers.Count / 2);

        for (int i = 0; i < maxCamps; i++)
        {
            var camp = sortedCamps[i];
            Vector2 campPos = new Vector2(
                camp.Item1.X * _beliefs.VisionMap.CellSize,
                camp.Item1.Y * _beliefs.VisionMap.CellSize
            );

            // Send 2-3 units to attack/capture each camp
            int unitsPerCamp = Mathf.Min(3, attackers.Count - unitsDeployed);

            for (int j = 0; j < unitsPerCamp; j++)
            {
                if (unitsDeployed >= attackers.Count) break;

                var attacker = attackers[unitsDeployed];

                // Add small offset to prevent crowding
                float offsetX = GD.RandRange(-30, 30);
                float offsetY = GD.RandRange(-30, 30);
                Vector2 finalPos = campPos + new Vector2(offsetX, offsetY);

                // Convert to grid position
                Vector2I gridPos = new Vector2I(
                    Mathf.RoundToInt(finalPos.X / _beliefs.VisionMap.CellSize),
                    Mathf.RoundToInt(finalPos.Y / _beliefs.VisionMap.CellSize)
                );

                // Check if position is already assigned
                if (!assignedPositions.Contains(gridPos))
                {
                    var fsm = attacker.GetNode<UnitFSM>("UnitFSM");
                    fsm.MoveTo(finalPos, UnitFSM.State.Moving);
                    assignedPositions.Add(gridPos);
                    _assignedUnits.Add(attacker);
                    unitsDeployed++;

                    // If unit is close enough, make it attack the camp directly
                    if (attacker is AttackableUnitBase attackableUnit &&
                        attacker.GlobalPosition.DistanceTo(campPos) < 150)
                    {
                        attackableUnit.GetNode<AttackableUnitFSM>("UnitFSM").AttackTarget(camp.Item2.EntityNode);
                    }
                }
            }
        }

        return unitsDeployed > 0 ? ExecutionStatus.Running : ExecutionStatus.Failure;
    }

    public override void Cleanup()
    {
        _assignedUnits.Clear();
    }
}