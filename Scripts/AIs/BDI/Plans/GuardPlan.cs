using System.Collections.Generic;
using System.Linq;
using Godot;
using NeuroWarCommander.Scripts.AIs.BDI.Base;
using NeuroWarCommander.Scripts.Units.Base;

namespace NeuroWarCommander.Scripts.AIs.BDI.Plans;

// Plan to capture neutral camps and resources
public class GuardPlan(BDIBeliefBase beliefs) : BDIPlan(beliefs)
{
    private float _rotationTimer = 0f;
    private const float RotationInterval = 2.0f;

    public override ExecutionStatus Execute(double delta, List<Vector2I> assignedPositions)
    {
        _rotationTimer += (float)delta;

        if (_rotationTimer < RotationInterval)
            return ExecutionStatus.Running;

        _rotationTimer = 0f;

        // Find all idle units
        var idleUnits = _beliefs.TeamNode
            .GetNode<Node>("Units")
            .GetChildren()
            .OfType<UnitBase>()
            .Where(u => u.GetNode<UnitFSM>("UnitFSM").GetCurrentState() == UnitFSM.State.Idle)
            .ToList();

        if (idleUnits.Count == 0)
            return ExecutionStatus.Success;

        // Make units rotate to scan surroundings
        foreach (var unit in idleUnits)
        {
            // Rotate units by random amount
            float rotation = (float)GD.RandRange(-Mathf.Pi / 2, Mathf.Pi / 2);
            unit.GetNode<UnitFSM>("UnitFSM").Rotate(rotation);
        }

        return ExecutionStatus.Running;
    }
}