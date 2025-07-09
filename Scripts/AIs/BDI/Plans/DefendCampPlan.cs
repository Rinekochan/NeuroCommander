using System.Collections.Generic;
using System.Linq;
using Godot;
using NeuroWarCommander.Scripts.AIs.BDI.Base;
using NeuroWarCommander.Scripts.Team.Blackboard;
using NeuroWarCommander.Scripts.Units.Base;
using NeuroWarCommander.Scripts.Units.Base.AttackableBase;

namespace NeuroWarCommander.Scripts.AIs.BDI.Plans;

// Plan to defend the team's main camp (a.k.a main base)
public class DefendCampPlan(BDIBeliefBase beliefs) : BDIPlan(beliefs)
{
    private List<UnitBase> _assignedUnits = [];

    public override ExecutionStatus Execute(double delta, List<Vector2I> assignedPositions)
    {
        if (_beliefs.TeamBasePosition == Vector2I.Zero)
            return ExecutionStatus.Failure;

        Vector2 baseWorldPos = new Vector2(
            _beliefs.TeamBasePosition.X * _beliefs.VisionMap.CellSize,
            _beliefs.TeamBasePosition.Y * _beliefs.VisionMap.CellSize
        );

        // Get all healthy attackers
        var defenders = _beliefs.HealthyAttackers.ToList();

        if (defenders.Count == 0)
            return ExecutionStatus.Failure;

        // Position defenders in a perimeter around the base
        int defendersDeployed = 0;
        int maxDefenders = Mathf.Min(defenders.Count, 8);

        for (int i = 0; i < maxDefenders; i++)
        {
            // Calculate positions in a circle around the base
            float angle = i * (Mathf.Pi * 2 / maxDefenders);
            float radius = 60.0f; // Adjust distance from base

            Vector2 defensePosition = baseWorldPos + new Vector2(
                Mathf.Cos(angle) * radius,
                Mathf.Sin(angle) * radius
            );

            // Find closest available defender
            var availableDefenders = defenders
                .Where(d => !_assignedUnits.Contains(d))
                .Where(d => d.GetNode<UnitFSM>("UnitFSM").GetCurrentState() == UnitFSM.State.Idle)
                .OrderBy(d => d.GlobalPosition.DistanceTo(defensePosition))
                .ToList();

            if (availableDefenders.Count == 0)
                continue;

            var defender = availableDefenders.First();
            Vector2I gridPosition = new Vector2I(
                Mathf.RoundToInt(defensePosition.X / _beliefs.LocationMap.CellSize),
                Mathf.RoundToInt(defensePosition.Y / _beliefs.LocationMap.CellSize)
            );

            if (!assignedPositions.Contains(gridPosition))
            {
                var fsm = defender.GetNode<UnitFSM>("UnitFSM");
                fsm.MoveTo(defensePosition, UnitFSM.State.Moving);
                assignedPositions.Add(gridPosition);
                _assignedUnits.Add(defender);
                defendersDeployed++;

                // Also check if we can see any enemies to attack
                var enemyUnits = _beliefs.LocationMap.GetAllUnits()
                    .Where(e => e.Item2.Type == LocationMap.EntityType.EnemyUnit)
                    .Where(e => defender.GlobalPosition.DistanceTo(e.Item2.EntityNode.GlobalPosition) < 250)
                    .OrderBy(e => defender.GlobalPosition.DistanceTo(e.Item2.EntityNode.GlobalPosition))
                    .ToList();

                if (enemyUnits.Count > 0 && defender is AttackableUnitBase attackableUnit)
                {
                    var target = enemyUnits.First().Item2.EntityNode;
                    attackableUnit.GetNode<AttackableUnitFSM>("UnitFSM").AttackTarget(target);
                }
            }
        }

        // If we've deployed defenders, continue monitoring
        return defendersDeployed > 0 ? ExecutionStatus.Running : ExecutionStatus.Success;
    }

    public override void Cleanup()
    {
        _assignedUnits.Clear();
    }
}