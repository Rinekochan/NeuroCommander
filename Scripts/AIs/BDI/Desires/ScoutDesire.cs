using Godot;
using NeuroWarCommander.Scripts.AIs.BDI.Base;
using NeuroWarCommander.Scripts.AIs.BDI.Plans;

namespace NeuroWarCommander.Scripts.AIs.BDI.Desires;

// Desire to gain information about unexplored areas
public class ScoutDesire(BDIBeliefBase beliefs) : BDIDesire(beliefs)
{
    public override bool IsRelevant()
    {
        return _beliefs.IsScoutingNeeded && _beliefs.HealthyScouts.Count > 0;
    }

    public override float CalculateUtility()
    {
        // Scouting utility increases with number of unexplored regions
        float baseUtility = 70.0f;
        float exploreMultiplier = Mathf.Min(1.0f, _beliefs.UnexploredRegions.Count / 20.0f);
        return baseUtility * exploreMultiplier;
    }

    public override BDIPlan GeneratePlan()
    {
        return new ScoutingPlan(_beliefs);
    }
}