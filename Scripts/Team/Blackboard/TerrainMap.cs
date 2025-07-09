using Godot;
using System.Collections.Generic;
using NeuroWarCommander.Scripts.Utils;

namespace NeuroWarCommander.Scripts.Team.Blackboard;

public enum TerrainType { Unknown, Granite, Mountain, Bridge, Gravel, Water }

public partial class TerrainMap : Node
{
    [Export] public Vector2I MapSize = new(2048, 2048);
    [Export] public int CellSize = 16;
    [Export] public Vector2I MapCenter = Vector2I.Zero;

    public Dictionary<Vector2I, TerrainType> TerrainCells { get; private set; } = new();

    private Rect2 _mapBounds;
    private Grid _grid; // Support for position-based grid operations

    public override void _Ready()
    {
        _grid = new Grid();
        int halfWidth = MapSize.X / 2;
        int halfHeight = MapSize.Y / 2;
        _mapBounds = new Rect2I(
            MapCenter.X - halfWidth,
            MapCenter.Y - halfHeight,
            MapSize.X,
            MapSize.Y
        );

        BuildGrid(_mapBounds); // Build terrain grid
    }

    public void Update(Vector2I position, int cellId)
    {
        // No need to update if the terrain type is already set
        if (!TerrainCells.ContainsKey(position) || TerrainCells[position] != TerrainType.Unknown) return;

        TerrainCells[position] = cellId switch
        {
            0 => TerrainType.Granite,
            1 => TerrainType.Mountain,
            2 => TerrainType.Bridge,
            3 => TerrainType.Bridge,
            4 => TerrainType.Gravel,
            5 => TerrainType.Mountain,
            6 => TerrainType.Water,
            7 => TerrainType.Water,
            _ => TerrainCells[(Vector2I)_grid.WorldToGrid(position)]
        };
    }

    public void BuildGrid(Rect2 bounds)
    {
        var startPos = -(MapSize / 2 / CellSize);
        var endPos = MapSize / 2 / CellSize;

        for (int x = startPos.X; x < endPos.X; x++)
        {
            for (int y = startPos.Y; y < endPos.Y; y++)
            {
                Vector2I pos = new Vector2I(x, y);

                TerrainCells[pos] = TerrainType.Unknown; // Initialize all cells as Unknown
            }
        }
    }

    public TerrainType GetTerrainTypeCellFromWorld(Vector2 worldPosition)
    {
        Vector2I gridPos = (Vector2I)_grid.WorldToGrid(worldPosition);
        return GetTerrainTypeCell(gridPos);
    }

    public TerrainType GetTerrainTypeCell(Vector2I gridPosition)
    {
        return TerrainCells.GetValueOrDefault(gridPosition, TerrainType.Unknown);
    }
}
