namespace NeuroWarCommander.Scripts.AIs.BehaviorTree.Base.Decorator;

// Repeater decorator: repeats child a fixed number of times or indefinitely.
// If repeatCount < 0, repeat forever (until a child returns Failure).
public class RepeaterNode(BTNode child, int repeatCount = -1) : DecoratorNode(child)
{
    private int _runCount = 0;

    public override NodeStatus Tick(double delta)
    {
        while (repeatCount < 0 || _runCount < repeatCount)
        {
            var status = Child.Tick(delta);
            if (status == NodeStatus.Running)
                return NodeStatus.Running;
            if (status == NodeStatus.Failure)
                return NodeStatus.Failure;
            // child succeeded, increment run count and repeat
            _runCount++;
        }
        // Completed required repeats
        _runCount = 0;
        return NodeStatus.Success;
    }
}