namespace NeuroWarCommander.Scripts.AIs.BehaviorTree.Base.Composite;

// Parallel node: executes all children simultaneously.
// Returns Success if the specified success policy is met.
public class ParallelNode(
    ParallelNode.Policy successPolicy,
    ParallelNode.Policy failurePolicy,
    params BTNode[] children)
    : CompositeNode(children)
{
    public enum Policy
    {
        RequireOne, // Succeed if at least one child succeeds
        RequireAll // Succeed only if all children succeed
    }

    public override NodeStatus Tick(double delta)
    {
        int successCount = 0;
        int failureCount = 0;

        // Execute all children
        foreach (var child in Children)
        {
            var status = child.Tick(delta);

            switch (status)
            {
                case NodeStatus.Success:
                    successCount++;
                    break;
                case NodeStatus.Failure:
                    failureCount++;
                    break;
            }
        }

        // Check if success policy is met
        if ((successPolicy == Policy.RequireOne && successCount > 0) ||
            (successPolicy == Policy.RequireAll && successCount == Children.Count))
        {
            return NodeStatus.Success;
        }

        // Check if failure policy is met
        if ((failurePolicy == Policy.RequireOne && failureCount > 0) ||
            (failurePolicy == Policy.RequireAll && failureCount == Children.Count))
        {
            return NodeStatus.Failure;
        }

        // Otherwise, still running (edge case)
        return NodeStatus.Running;
    }
}