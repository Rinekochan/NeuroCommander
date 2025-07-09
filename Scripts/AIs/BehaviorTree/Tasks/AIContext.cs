using System.Collections.Generic;
using Godot;
using NeuroWarCommander.Scripts.Team.Blackboard;
using NeuroWarCommander.Scripts.Utils;

namespace NeuroWarCommander.Scripts.AIs.BehaviorTree.Tasks;

public record AIContext(
    Blackboard blackBoard,
    VisionMap visionMap,
    LocationMap locationMap,
    InfluenceMap influenceMap,
    TerrainMap terrainMap,
    Team.Team teamNode,
    Map map,
    List<Vector2I> assignedPositon
);