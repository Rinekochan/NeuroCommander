namespace NeuroWarCommander.Scripts.AIs.BDI.Base;

public abstract class BDIDesire(BDIBeliefBase beliefs)
{
    protected readonly BDIBeliefBase _beliefs = beliefs;

    // Determines if this desire is relevant given current beliefs
    public abstract bool IsRelevant();

    // Calculates how desirable this goal is in the current situation
    public abstract float CalculateUtility();

    // Generates a concrete plan to achieve this desire
    public abstract BDIPlan GeneratePlan();
}