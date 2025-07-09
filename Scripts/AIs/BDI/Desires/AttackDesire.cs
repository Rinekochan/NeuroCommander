using Godot;
using NeuroWarCommander.Scripts.AIs.BDI.Base;
using NeuroWarCommander.Scripts.AIs.BDI.Plans;

namespace NeuroWarCommander.Scripts.AIs.BDI.Desires;

// Desire to attack enemy positions
public class AttackDesire : BDIDesire
{
    public AttackDesire(BDIBeliefBase beliefs) : base(beliefs) { }

    public override bool IsRelevant()
    {
        return (_beliefs.IsMarchOpportunityAvailable || _beliefs.IsSeeingTheirCommander) && _beliefs.HealthyAttackers.Count > 1;
    }

    public override float CalculateUtility()
    {
        // Higher utility if we have many healthy attackers, and there's many enemy nearby
        float baseUtility = 70.0f;
        float strengthMultiplier = _beliefs.AverageTeamHealth * (_beliefs.HealthyAttackers.Count / 5.0f) + (_beliefs.EnemyInfluenceHotspots.Count / 5.0f);;
        return baseUtility * Mathf.Min(1.0f, strengthMultiplier) + (_beliefs.IsSeeingTheirCommander ? 30.0f : 0.0f); // Add bonus if we see their commander
    }

    public override BDIPlan GeneratePlan()
    {
        return new AssaultPlan(_beliefs);
    }
}