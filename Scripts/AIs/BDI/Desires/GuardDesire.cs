using NeuroWarCommander.Scripts.AIs.BDI.Base;
using NeuroWarCommander.Scripts.AIs.BDI.Plans;

namespace NeuroWarCommander.Scripts.AIs.BDI.Desires;

// Default desire when no other desires are relevant
public class GuardDesire(BDIBeliefBase beliefs) : BDIDesire(beliefs)
{
    public override bool IsRelevant()
    {
        // Always relevant as a fallback
        return true;
    }

    public override float CalculateUtility()
    {
        // Lowest priority
        return 10.0f;
    }

    public override BDIPlan GeneratePlan()
    {
        return new GuardPlan(_beliefs);
    }
}