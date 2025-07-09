using Godot;
using NeuroWarCommander.Scripts.AIs.BehaviorTree.Base;
using NeuroWarCommander.Scripts.Units.Base;

namespace NeuroWarCommander.Scripts.AIs.BehaviorTree.Tasks.Action;

/// All units stop and do a slight rotation sweep in place to guard their position.
public class ActionOrderGuard(AIContext context) : BTNode
{
    public override NodeStatus Tick(double delta)
    {
        // For each alive unit in this team, call Stop() and rotate them slightly
        foreach (var child in context.teamNode.GetNode<Node>("Units").GetChildren())
        {
            if (!(child is UnitBase unit)) continue;
            if (unit.CurrentHealth <= 0) continue;

            var fsm = unit.GetNode<UnitFSM>("UnitFSM");
            if (fsm.GetCurrentState() != UnitFSM.State.Idle)
                fsm.Stop();

            // Oscillate 15° over a 2‐second sine wave
            float t = Time.GetTicksMsec() / 1000.0f;
            float offset = Mathf.Sin(t * Mathf.Pi) * Mathf.DegToRad(15);
            float newAngle = unit.Rotation + offset;
            fsm.RotateToAngle(newAngle);
        }
        return NodeStatus.Success;
    }
}