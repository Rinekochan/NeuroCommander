using System.Collections.Generic;
using System.Linq;
using Godot;
using NeuroWarCommander.Scripts.AIs.BehaviorTree;
using NeuroWarCommander.Scripts.Camps;
using NeuroWarCommander.Scripts.Team.Blackboard;
using NeuroWarCommander.Scripts.Units.Base;
using NeuroWarCommander.Scripts.Units.Base.AttackableBase;
using NeuroWarCommander.Scripts.Utils;

namespace NeuroWarCommander.Scripts.AIs.BDI.Base;

public class BDIBeliefBase(
    Blackboard blackBoard,
    VisionMap visionMap,
    LocationMap locationMap,
    InfluenceMap influenceMap,
    TerrainMap terrainMap,
    Map map,
    Team.Team teamNode)
{
    // Core references
    public Blackboard BlackBoard { get; private set; } = blackBoard;
    public VisionMap VisionMap { get; } = visionMap;
    public LocationMap LocationMap { get; } = locationMap;
    public InfluenceMap InfluenceMap { get; } = influenceMap;
    public TerrainMap TerrainMap { get; private set; } = terrainMap;
    public Map Map { get; } = map;
    public Team.Team TeamNode { get; } = teamNode;

    // Derived beliefs about world state
    public bool AreLowHealthUnitsPresent { get; private set; }
    public bool IsMarchOpportunityAvailable { get; private set; }
    public bool IsScoutingNeeded { get; private set; }
    public bool IsCampUnderAttack { get; private set; }
    public bool IsSeeingTheirCommander { get; private set; }
    public List<Vector2I> EnemyInfluenceHotspots { get; private set; } = [];
    public List<Vector2I> UnexploredRegions { get; private set; } = [];
    public Vector2I BestAttackTarget { get; private set; }
    public Vector2I BestScoutTarget { get; private set; }
    public List<AttackableUnitBase> HealthyAttackers { get; private set; } = [];
    public List<UnitBase> HealthyScouts { get; private set; } = [];
    public Vector2I TeamBasePosition { get; private set; }
    public Vector2I TheirCommanderPosition { get; private set; }
    public int FriendlyUnitCount { get; private set; }
    public float AverageTeamHealth { get; private set; }

    public void Update(double delta)
    {
        // Update unit-related beliefs
        UpdateUnitBeliefs();

        // Update strategic beliefs
        UpdateStrategicBeliefs();

        UpdateResourceBeliefs();
    }

    private void UpdateUnitBeliefs()
    {
        // Get all our units
        var allUnits = TeamNode.GetNode<Node>("Units").GetChildren().OfType<UnitBase>().ToList();

        FriendlyUnitCount = allUnits.Count;

        // Check for low health units
        int lowHealthCount = allUnits.Count(u => u.CurrentHealth < u.MaxHealth * 0.3f);
        AreLowHealthUnitsPresent = lowHealthCount > 0;

        // Find average team health
        float totalHealth = 0f;
        float totalMaxHealth = 0f;
        foreach (var unit in allUnits)
        {
            totalHealth += unit.CurrentHealth;
            totalMaxHealth += unit.MaxHealth;
        }
        AverageTeamHealth = allUnits.Count > 0 ? totalHealth / totalMaxHealth : 0;

        // Find healthy attackers and scouts
        HealthyAttackers = allUnits
            .OfType<AttackableUnitBase>()
            .Where(a => a.CurrentHealth > a.MaxHealth * 0.3f && !a.IsWeaponReloading())
            .ToList();

        HealthyScouts = allUnits
            .Where(s => s.CurrentHealth > s.MaxHealth * 0.25f && s.IsInGroup("scouts"))
            .ToList();
    }

    private void UpdateStrategicBeliefs()
    {
        // Find enemy influence hotspots
        EnemyInfluenceHotspots = InfluenceMap.InfluenceCells
            .Where(kvp => kvp.Value.Confidence)
            .Where(kvp => kvp.Value.TotalInfluence < 0.0f)
            .Select(kvp => kvp.Key)
            .ToList();

        IsMarchOpportunityAvailable = EnemyInfluenceHotspots.Count > 0;

        if (EnemyInfluenceHotspots.Count > 0)
        {
            // Determine best attack target based on strength and distance
            BestAttackTarget = FindBestAttackTarget();
        }

        // Check if we can see their commander
        IsSeeingTheirCommander = LocationMap
            .GetAllUnits()
            .Where(e => e.Item2.Type == LocationMap.EntityType.EnemyUnit)
            .Select(e => e.Item2.EntityNode)
            .OfType<UnitBase>()
            .Where(GodotObject.IsInstanceValid)
            .Any(c => c.IsInGroup("commanders"));

        if (IsSeeingTheirCommander)
        {
            // If we see their commander, update its position
            TheirCommanderPosition = (Vector2I)LocationMap
                .GetAllUnits()
                .Where(e => e.Item2.Type == LocationMap.EntityType.EnemyUnit && e.Item2.EntityNode is UnitBase commander && commander.IsInGroup("commanders"))
                .Select(e => e.Item2.EntityNode.GlobalPosition)
                .FirstOrDefault();
        }
        else
        {
            TheirCommanderPosition = Vector2I.Zero; // Reset if not seeing their commander
        }

        // Find unexplored regions or regions where vision is old
        UnexploredRegions = FindUnexploredRegions();
        IsScoutingNeeded = UnexploredRegions.Count > 0;

        if (IsScoutingNeeded)
        {
            BestScoutTarget = FindBestScoutTarget();
        }

        // Detect if our base is under attack
        IsCampUnderAttack = DetectCampUnderAttack();
    }

    private void UpdateResourceBeliefs()
    {
        // Update team base position
        var ownCamps = LocationMap.GetAllAllyCamps().ToList();
        if (ownCamps.Count > 0)
        {
            // The main base will be the one with the highest max health
            var mainBase = ownCamps
                .Select(c => (c.Item1, c.Item2.EntityNode as CampBase))
                .Where(c => c.Item2 != null)
                .OrderByDescending(c => c.Item2.MaxHealth)
                .FirstOrDefault();

            if (mainBase.Item2 != null)
            {
                TeamBasePosition = mainBase.Item1;
            }
            else
            {
                TeamBasePosition = ownCamps.First().Item1;
            }
        }
    }

    private Vector2I FindBestAttackTarget()
    {
        // We will exploit the weakest enemy influence hotspot
        var bestTarget = EnemyInfluenceHotspots
            .OrderByDescending(pos => InfluenceMap.InfluenceCells[pos].TotalInfluence)
            .FirstOrDefault();

        if (IsSeeingTheirCommander)
        {
            bestTarget = TheirCommanderPosition / InfluenceMap.CellSize;
        }


        return bestTarget;
    }

    private Vector2I FindBestScoutTarget()
    {
        var filteredRegions = UnexploredRegions
            .Where(pos =>
            {
                // Check nearby cells for entities
                bool isOccupied = false;
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        Vector2I checkPos = pos + new Vector2I(dx, dy);
                        var cell = LocationMap.GetLocationCell(checkPos);
                        if (cell?.EntityNode != null)
                        {
                            isOccupied = true;
                            break;
                        }
                    }
                    if (isOccupied) break;
                }
                return !isOccupied;
            })
            .ToList();

        if (filteredRegions.Count == 0 && UnexploredRegions.Count > 0)
        {
            return UnexploredRegions.First();
        }

        return filteredRegions.Count > 0 ? filteredRegions[0] : Vector2I.Zero;
    }

    private List<Vector2I> FindUnexploredRegions()
    {
        var unexplored = new List<Vector2I>();

        // Find the positions of scouts
        var scoutPositions = HealthyScouts
            .Select(s => s.GlobalPosition)
            .ToHashSet();

        var averageScoutPosition = scoutPositions.Count > 0
            ? new Vector2I((int)scoutPositions.Average(pos => pos.X), (int)scoutPositions.Average(pos => pos.Y))
            : Vector2I.Zero;

        // Sort unexplored regions by distance to scouts
        foreach (var pos in VisionMap.VisionCells.Keys)
        {
            if (!Map.GetGrid().GetCell(pos).Walkable) continue; // Skip non-walkable cells
            float lastSeen = VisionMap.GetVisionTimeOfCell(pos);
            if (lastSeen is >= float.MaxValue - 1.0f or > 15.0f)
            {
                unexplored.Add(pos);
            }
        }

        // Sort by how long ago they were seen, and the distance to the average scout position
        return unexplored
            .Where(pos => pos.DistanceTo(averageScoutPosition /VisionMap.CellSize) < 64) // Not too far
            .Where(pos => pos.DistanceTo(averageScoutPosition / VisionMap.CellSize) > 15) // Not too close
            .OrderByDescending(pos => VisionMap.GetVisionTimeOfCell(pos))
            .ThenBy(pos => pos.DistanceTo(averageScoutPosition / VisionMap.CellSize)) // Prioritize cells closer to scouts
            .ToList();
    }

    private bool DetectCampUnderAttack()
    {
        if (TeamBasePosition == Vector2I.Zero) return false;

        // Check if enemy units are near our camp
        var enemyUnitsNearCamp = LocationMap
            .GetAllUnits()
            .Where(e => e.Item2.Type == LocationMap.EntityType.EnemyUnit)
            .Any(e => e.Item1.DistanceTo(TeamBasePosition) < 10);

        return enemyUnitsNearCamp;
    }
}