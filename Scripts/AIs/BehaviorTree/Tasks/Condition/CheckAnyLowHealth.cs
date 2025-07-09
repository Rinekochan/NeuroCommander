using System.Linq;
using Godot;
using NeuroWarCommander.Scripts.AIs.BehaviorTree.Base;
using NeuroWarCommander.Scripts.Units.Base;

namespace NeuroWarCommander.Scripts.AIs.BehaviorTree.Tasks.Condition;

/// Check if any unit is low on health (below LowHealthFraction).
/// Returns Success if health is low (so we retreat), else Failure.
public class CheckAnyLowHealth(AIContext context) : BTNode
{
    private const float LowHealthFrac = 0.30f;

    public override NodeStatus Tick(double delta)
    {
        // If any UnitBase under Team/Units has health < threshold:
        var anyLow = context.teamNode
            .GetNode<Node>("Units")
            .GetChildren()
            .OfType<UnitBase>()
            .Any(u => u.CurrentHealth / u.MaxHealth <= LowHealthFrac && u.CurrentHealth > 0);

        // GD.Print($"CheckAnyLowHealth: any low health? {anyLow}");

        return anyLow ? NodeStatus.Success : NodeStatus.Failure;
    }
}