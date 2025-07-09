using Godot;
using System.Collections.Generic;
using System.Linq;
using NeuroWarCommander.Scripts.Utils;

namespace NeuroWarCommander.Scripts.Team.Blackboard;

public partial class VisionMap : Node
{
    [Export] public Vector2I MapSize = new(2048, 2048);
    [Export] public int CellSize = 16;
    [Export] public Vector2I MapCenter = Vector2I.Zero;

    public Dictionary<Vector2I, float> VisionCells { get; private set; } = new();

    private Rect2 _mapBounds;
    private Grid _grid; // Support us with position conversion

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

        BuildGrid(); // Build vision grid
    }

    public override void _Process(double delta)
    {
        if (VisionCells.Count == 0 && VisionCells is not null)
        {
            GD.PrintErr("VisionMap: No vision cells initialized. Ensure the grid is built before processing.");
            return;
        }

        // Update vision cells by incrementing their vision time
        foreach (var key in VisionCells.Keys.ToList())
        {
            if (VisionCells[key] < float.MaxValue)
            {
                VisionCells[key] += (float)delta; // Increment vision time by delta seconds
            }
        }
    }

    public void Update(Vector2I position)
    {
        if (!VisionCells.ContainsKey(position)) return; // Handle edge cases

        VisionCells[position] = 0.0f; // Set vision time to 0 for the cell at the given position
    }

    public void BuildGrid()
    {
        var startPos = -(MapSize / 2 / CellSize);
        var endPos = MapSize / 2 / CellSize;

        for (int x = startPos.X; x < endPos.X; x++)
        {
            for (int y = startPos.Y; y < endPos.Y; y++)
            {
                Vector2I pos = new Vector2I(x, y);
                VisionCells[pos] = float.MaxValue; // Initialize all cells as infinite seconds of vision
            }
        }
    }

    public float GetVisionTimeOfCellFromWorld(Vector2 worldPosition)
    {
        Vector2I gridPos = (Vector2I)_grid.WorldToGrid(worldPosition);
        return GetVisionTimeOfCell(gridPos);
    }

    public float GetVisionTimeOfCell(Vector2I gridPosition)
    {
        return VisionCells.GetValueOrDefault(gridPosition, float.MaxValue);
    }

    public void VisualiseGrid()
    {
        var startPos = -(MapSize / 2 / CellSize);
        var endPos = MapSize / 2 / CellSize;

        for (int x = startPos.X; x < endPos.X; x++)
        {
            string rowText = "";
            for (int y = startPos.Y; y < endPos.Y; y++)
            {
                Vector2I pos = new Vector2I(x, y);
                var visionTime = VisionCells[pos] == float.MaxValue ? "I" : VisionCells[pos].ToString("F1");
                rowText += $"{visionTime}|";
            }
            GD.Print(rowText);
            GD.Print(
                "-------------------------------------------------------------------------------------------------------------");
        }
    }
}
