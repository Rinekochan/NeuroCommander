using System.Collections.Generic;
using Godot;

namespace NeuroWarCommander.Scripts.AIs.BDI.Base;

// Base class for concrete plans that achieve desires
public abstract class BDIPlan(BDIBeliefBase beliefs)
{
    protected readonly BDIBeliefBase _beliefs = beliefs;

    public enum ExecutionStatus
    {
        Running,
        Success,
        Failure
    }

    // Execute one step of the plan
    public abstract ExecutionStatus Execute(double delta, List<Vector2I> assignedPositions);

    // Clean up any resources when the plan terminates
    public virtual void Cleanup() { }
}