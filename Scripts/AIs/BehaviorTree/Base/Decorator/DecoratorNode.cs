namespace NeuroWarCommander.Scripts.AIs.BehaviorTree.Base.Decorator;

// Decorator node: has a single child and modifies its behavior.
// Decorators can change the status of the child node or add additional logic before or after the child's execution.
public abstract class DecoratorNode(BTNode child) : BTNode
{
    protected readonly BTNode Child = child;
}