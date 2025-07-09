namespace NeuroWarCommander.Scripts.AIs.BDI.Base;

// An intention represents a desire that the agent has committed to pursuing
public class BDIIntention(BDIPlan plan, BDIDesire desire, float timeout = 10.0f)
{
    public BDIPlan Plan { get; } = plan;
    public BDIDesire Desire { get; } = desire;
    public float TimeoutDuration { get; } = timeout;

    public bool IsValid()
    {
        // An intention is valid as long as its underlying desire is relevant
        return Desire.IsRelevant();
    }
}