using Godot;
using NeuroWarCommander.Scripts.AIs.BDI.Base;
using NeuroWarCommander.Scripts.AIs.BDI.Plans;

namespace NeuroWarCommander.Scripts.AIs.BDI.Desires;

// Desire to protect low health units by retreating them
public class SurvivalDesire(BDIBeliefBase beliefs) : BDIDesire(beliefs)
{
    public override bool IsRelevant()
    {
        return _beliefs.AreLowHealthUnitsPresent && _beliefs.TeamBasePosition != Vector2I.Zero;
    }

    public override float CalculateUtility()
    {
        // Survival is highest priority
        return 100.0f;
    }

    public override BDIPlan GeneratePlan()
    {
        return new RetreatPlan(_beliefs);
    }
}