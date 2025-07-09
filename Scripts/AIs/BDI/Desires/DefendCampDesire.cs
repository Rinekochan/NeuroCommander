using NeuroWarCommander.Scripts.AIs.BDI.Base;
using NeuroWarCommander.Scripts.AIs.BDI.Plans;

namespace NeuroWarCommander.Scripts.AIs.BDI.Desires;

// Desire to defend the team's camp
public class DefendCampDesire(BDIBeliefBase beliefs) : BDIDesire(beliefs)
{
    public override bool IsRelevant()
    {
        return _beliefs.IsCampUnderAttack;
    }

    public override float CalculateUtility()
    {
        return 65.0f + (_beliefs.IsCampUnderAttack ? 5.0f : 0.0f); // Increase if enemy forces are nearby
    }

    public override BDIPlan GeneratePlan()
    {
        return new DefendCampPlan(_beliefs);
    }
}