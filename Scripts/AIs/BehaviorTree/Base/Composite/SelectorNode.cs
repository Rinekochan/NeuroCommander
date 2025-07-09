using Godot;

namespace NeuroWarCommander.Scripts.AIs.BehaviorTree.Base.Composite;

/// Selector node: executes children in order until one succeeds.
/// Returns Success as soon as one child returns Success.
public class SelectorNode(params BTNode[] children) : CompositeNode(children)
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
                case NodeStatus.Success:
                    _current = 0;
                    return NodeStatus.Success;
                case NodeStatus.Failure:
                default:
                    // child failed → try next
                    _current++;
                    break;
            }
        }
        // all children failed
        _current = 0;
        return NodeStatus.Failure;
    }
}