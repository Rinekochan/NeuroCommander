using System.Linq;
using Godot;
using NeuroWarCommander.Scripts.AIs.BDI.Base;
using NeuroWarCommander.Scripts.AIs.BDI.Plans;

namespace NeuroWarCommander.Scripts.AIs.BDI.Desires;

// Desire to attack neutral camps
public class CaptureDesire(BDIBeliefBase beliefs) : BDIDesire(beliefs)
{
    public override bool IsRelevant()
    {
        // We need at least some units to capture
        if (_beliefs.HealthyAttackers.Count < 2) return false;

        // Check if we can see any neutral or enemy camps
        var neutralCamps = _beliefs.LocationMap.GetAllNeutralCamps().ToList();
        var enemyCamps = _beliefs.LocationMap.GetAllEnemyCamps().ToList();
        return neutralCamps.Count > 0 || enemyCamps.Count > 0;
    }

    public override float CalculateUtility()
    {
        var neutralCamps = _beliefs.LocationMap.GetAllNeutralCamps().ToList();
        var enemyCamps = _beliefs.LocationMap.GetAllEnemyCamps().ToList();

        float baseUtility = 65.0f;

        return baseUtility * (1.0f + neutralCamps.Count * 0.1f + enemyCamps.Count * 0.1f);
    }

    public override BDIPlan GeneratePlan()
    {
        return new CapturePlan(_beliefs);
    }
}