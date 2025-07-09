using Godot;
using System.Linq;
using NeuroWarCommander.Scripts.AIs.BehaviorTree.Base;
using NeuroWarCommander.Scripts.AIs.BehaviorTree.Base.Composite;
using NeuroWarCommander.Scripts.AIs.BehaviorTree.Tasks;
using NeuroWarCommander.Scripts.AIs.BehaviorTree.Tasks.Action;
using NeuroWarCommander.Scripts.AIs.BehaviorTree.Tasks.Condition;
using NeuroWarCommander.Scripts.Team.Blackboard;
using NeuroWarCommander.Scripts.Units.Base.AttackableBase;
using NeuroWarCommander.Scripts.Utils;

namespace NeuroWarCommander.Scripts.AIs.BehaviorTree;

public partial class BehaviorTreeAI : Node2D
{
    private const float TickInterval = 1.25f;
    private float _timeAcc = 0f;

    private Blackboard _bb;
    private VisionMap _visionMap;
    private LocationMap _locationMap;
    private InfluenceMap _influenceMap;
    private TerrainMap _terrainMap;
    private Team.Team _teamNode;
    private Map _map;

    private BTNode _rootNode;

    public override void _Ready()
    {
        _teamNode = GetParent<Team.Team>();
        _map = _teamNode.GetParent().GetNode<Map>("Map");
        _bb = _teamNode.GetNode<Blackboard>("Blackboard");
        _visionMap = _bb.GetNode<VisionMap>("VisionMap");
        _locationMap = _bb.GetNode<LocationMap>("LocationMap");
        _influenceMap = _bb.GetNode<InfluenceMap>("InfluenceMap");
        _terrainMap = _bb.GetNode<TerrainMap>("TerrainMap");

        var ctx = new AIContext(_bb, _visionMap, _locationMap, _influenceMap, _terrainMap, _teamNode, _map, []);
        _rootNode = BuildBehaviorTree(ctx);

        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        _timeAcc += (float)delta;
        if (_timeAcc < TickInterval) return;
        _timeAcc = 0f;

        _rootNode.Tick(delta);
    }

    private BTNode BuildBehaviorTree(AIContext context)
    {
        // Check any low‐health & order retreat
        var checkLow = new CheckAnyLowHealth(context);
        var actionRet = new ActionOrderRetreat(context);
        var lowHealthSeq = new SequenceNode(checkLow, actionRet);

        // Check march opportunity via InfluenceMap & order attack
        var checkMrc = new CheckMarchOpportunity(context);
        var actionMrc = new ActionOrderMarch(context, checkMrc);
        var mrcSeq = new SequenceNode(checkMrc, actionMrc);
        var actionAtk = new ActionOrderAttack(context, context.teamNode.GetNode("Units").GetChildren().Count(u => u is AttackableUnitBase));

        // Check scout need & order scout
        var checkScout = new CheckScoutNeed(context);
        var actionScoutParallel = new ParallelNode(
            ParallelNode.Policy.RequireOne,
            ParallelNode.Policy.RequireAll,
            new ActionOrderScout(context, checkScout, context.teamNode.GetNode("Units").GetChildren().Count(u => u.IsInGroup("scouts")))
        );
        var scoutSeq = new SequenceNode(checkScout, actionScoutParallel);

        var strategyParallel = new ParallelNode(
            ParallelNode.Policy.RequireOne,
            ParallelNode.Policy.RequireAll,
            lowHealthSeq,
            scoutSeq,
            mrcSeq,
            actionAtk
        );

        // Default guard (stop + rotate)
        var guardAct = new ActionOrderGuard(context);

        // Root selector (in priority order)
        return new SelectorNode(
            strategyParallel,
            guardAct
        );
    }
}