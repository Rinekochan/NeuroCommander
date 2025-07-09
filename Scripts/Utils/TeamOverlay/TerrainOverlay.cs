using Godot;
using System.Collections.Generic;
using NeuroWarCommander.Scripts.Team.Blackboard;

namespace NeuroWarCommander.Scripts.Utils.TeamOverlay;

public partial class TerrainOverlay : Node2D
{
    private TerrainMap _terrainMap;
    private float _cellSize;
    private const float MinimumUpdatedInterval = 1.5f; // minimum time between updates to avoid lagging
    private float _lastUpdateTime = 0.0f;


    public override void _Ready()
    {
        var bb = GetParent().GetParent().GetParent().GetNode<Blackboard>("Blackboard");
        _terrainMap = bb.GetNode<TerrainMap>("TerrainMap");
        _cellSize = _terrainMap.CellSize;

        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        _lastUpdateTime += (float)delta;
        if (_lastUpdateTime >= MinimumUpdatedInterval)
        {
            _lastUpdateTime = 0.0f; // reset the timer
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        // Loop through every cell in TerrainMap.TerrainCells
        foreach (var kvp in _terrainMap.TerrainCells)
        {
            Vector2I gridPosF = kvp.Key;
            TerrainType type  = kvp.Value;

            switch (type)
            {
                case TerrainType.Unknown:
                    continue; // Skip unknown terrain types
                case TerrainType.Granite:
                    DrawTerrainCell(gridPosF, Colors.Orange);
                    break;
                case TerrainType.Mountain:
                    DrawTerrainCell(gridPosF, Colors.Brown);
                    break;
                case TerrainType.Bridge:
                    DrawTerrainCell(gridPosF, Colors.Red);
                    break;
                case TerrainType.Gravel:
                    DrawTerrainCell(gridPosF, Colors.Gray);
                    break;
                case TerrainType.Water:
                    DrawTerrainCell(gridPosF, Colors.Blue);
                    break;
            }
        }
    }

    private void DrawTerrainCell(Vector2I gridPosF, Color color)
    {
        Vector2 pos = new Vector2(gridPosF.X * _cellSize, gridPosF.Y * _cellSize);
        Vector2 globalPos = ToLocal(pos);

        // We will use outline box to represent the terrain type
        DrawRect(new Rect2(globalPos + Vector2.One, new Vector2(_cellSize - 1, _cellSize - 1)), color, filled: false);
    }
}

