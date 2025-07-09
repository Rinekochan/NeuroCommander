namespace NeuroWarCommander.Scripts.AIs.BehaviorTree.Base;

public enum NodeStatus
{
    Success,
    Failure,
    Running
}

public abstract class BTNode
{
    public abstract NodeStatus Tick(double delta);
}