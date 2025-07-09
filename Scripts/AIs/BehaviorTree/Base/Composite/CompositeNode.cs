using System.Collections.Generic;
using System.Linq;

namespace NeuroWarCommander.Scripts.AIs.BehaviorTree.Base.Composite;

// Composite node: has multiple children.
public abstract class CompositeNode(params BTNode[] children) : BTNode
{
    protected readonly List<BTNode> Children = children.ToList();

    public void AddChild(BTNode child) => Children.Add(child);
}