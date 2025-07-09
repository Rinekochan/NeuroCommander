namespace NeuroWarCommander.Scripts.AIs.BehaviorTree.Base.Composite;

/// Sequence node: executes children in order until one fails.
/// Returns Success only if all children succeed.
public class SequenceNode(params BTNode[] children) : CompositeNode(children)
{
    private int _current = 0;

    public override NodeStatus Tick(double delta)
    {
        while (_current < Children.Count)
        {
            var status = Children[_current].Tick(delta);
            switch (status)
            {
                case NodeStatus.Running:
                    return NodeStatus.Running;
                case NodeStatus.Failure:
                    _current = 0;
                    return NodeStatus.Failure;
                case NodeStatus.Success:
                default:
                    // child succeeded → advance
                    _current++;
                    break;
            }
        }
        // all children succeeded
        _current = 0;
        return NodeStatus.Success;
    }
}