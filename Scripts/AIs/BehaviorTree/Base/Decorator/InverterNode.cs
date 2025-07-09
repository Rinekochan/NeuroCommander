namespace NeuroWarCommander.Scripts.AIs.BehaviorTree.Base.Decorator;

/// Inverter decorator: inverts child’s result (Success to Failure and vice versa).
public class InverterNode(BTNode child) : DecoratorNode(child)
{
    public override NodeStatus Tick(double delta)
    {
        var status = Child.Tick(delta);
        return status switch
        {
            NodeStatus.Success => NodeStatus.Failure,
            NodeStatus.Failure => NodeStatus.Success,
            _ => NodeStatus.Running
        };
    }
}
