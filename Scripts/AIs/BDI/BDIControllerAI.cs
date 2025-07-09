using Godot;
using System.Collections.Generic;
using System.Linq;
using NeuroWarCommander.Scripts.AIs.BDI.Base;
using NeuroWarCommander.Scripts.AIs.BDI.Desires;
using NeuroWarCommander.Scripts.Team.Blackboard;
using NeuroWarCommander.Scripts.Utils;

namespace NeuroWarCommander.Scripts.AIs.BDI;

public partial class BDIControllerAI : Node2D
{
    private const float DeliberationInterval = 1.25f;
    private float _timeAcc = 0f;

    private Team.Team _teamNode;
    private Blackboard _bb;
    private VisionMap _visionMap;
    private LocationMap _locationMap;
    private InfluenceMap _influenceMap;
    private TerrainMap _terrainMap;
    private Map _map;

    // Belief base
    private BDIBeliefBase _beliefs;

    // Desire set
    private List<BDIDesire> _desires = [];

    // Current intention
    private BDIIntention _currentIntention;
    private float _intentionTimeoutAccumulator = 0f;

    private List<Vector2I> _assignedPositions = [];

    public override void _Ready()
    {
        _teamNode = GetParent<Team.Team>();
        _bb = _teamNode.GetNode<Blackboard>("Blackboard");
        _visionMap = _bb.GetNode<VisionMap>("VisionMap");
        _locationMap = _bb.GetNode<LocationMap>("LocationMap");
        _influenceMap = _bb.GetNode<InfluenceMap>("InfluenceMap");
        _terrainMap = _bb.GetNode<TerrainMap>("TerrainMap");

        _map = _teamNode.GetParent().GetNode<Map>("Map");

        _beliefs = new BDIBeliefBase(_bb, _visionMap, _locationMap, _influenceMap, _terrainMap, _map, _teamNode);

        InitializeDesires();

        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        _timeAcc += (float)delta;

        // Update beliefs every frame
        _beliefs.Update(delta);

        if (_timeAcc < DeliberationInterval)
        {
            ExecuteCurrentIntention(delta);
            return;
        }

        _timeAcc = 0f;

        // BDI reasoning cycle
        DeliberateAndAct();
    }

    private void InitializeDesires()
    {
        // Core desires, sorted by priority (highest first)
        _desires.Add(new SurvivalDesire(_beliefs));
        _desires.Add(new DefendCampDesire(_beliefs));
        _desires.Add(new ScoutDesire(_beliefs));
        _desires.Add(new AttackDesire(_beliefs));
        _desires.Add(new CaptureDesire(_beliefs));
        _desires.Add(new GuardDesire(_beliefs));
    }

    private void DeliberateAndAct()
    {
        // Check if current intention is still valid
        if (_currentIntention != null)
        {
            if (!_currentIntention.IsValid())
            {
                TerminateCurrentIntention();
            }
        }

        // If no intention or current one just terminated, select a new one
        if (_currentIntention == null)
        {
            SelectNewIntention();
        }
    }

    private void SelectNewIntention()
    {
        // Filter relevant desires based on current beliefs
        var relevantDesires = _desires.Where(d => d.IsRelevant()).ToList();

        // Find the highest utility desire
        BDIDesire bestDesire = null;
        float bestUtility = float.MinValue;

        foreach (var desire in relevantDesires)
        {
            float utility = desire.CalculateUtility();
            if (utility > bestUtility)
            {
                bestUtility = utility;
                bestDesire = desire;
            }
        }

        if (bestDesire != null)
        {
            var plan = bestDesire.GeneratePlan();
            if (plan != null)
            {
                _currentIntention = new BDIIntention(plan, bestDesire);
                GD.Print($"BDI: New intention adopted: {bestDesire.GetType().Name}");

                // Clean assigned positions
                _assignedPositions.Clear();
            }
        }
    }

    private void ExecuteCurrentIntention(double delta)
    {
        if (_currentIntention == null) return;

        // Check timeout for current intention
        _intentionTimeoutAccumulator += (float)delta;
        if (_intentionTimeoutAccumulator > _currentIntention.TimeoutDuration)
        {
            GD.Print($"BDI: Intention timed out: {_currentIntention.Desire.GetType().Name}");
            TerminateCurrentIntention();
            return;
        }

        // Execute the plan steps
        _currentIntention.Plan.Execute(delta, _assignedPositions);
    }

    private void TerminateCurrentIntention()
    {
        if (_currentIntention == null) return;
        _currentIntention.Plan.Cleanup();
        _currentIntention = null;
        _intentionTimeoutAccumulator = 0f;
    }
}

