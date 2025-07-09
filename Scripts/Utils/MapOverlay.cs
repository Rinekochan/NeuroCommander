using Godot;
using System;

namespace NeuroWarCommander.Scripts.Utils;

public partial class MapOverlay : Node2D
{

    [Export] private Color _walkableColor = new Color(0, 1, 0, 0.3f);
    [Export] private Color _unwalkableColor = new Color(1, 0, 0, 0.5f);
    [Export] private Color _connectionColor = new Color(0, 0, 1, 0.2f);
    [Export] private Color _textColor = new Color(1, 1, 1, 0.7f);

    [Export] private float _nodeRadius = 4f;
    [Export] private float _connectionWidth = 1f;
    [Export] private bool _showCosts = true;
    [Export] private bool _showConnections = true;

    private Grid _grid;

    public override void _Ready()
    {
        // Configuration is done through SetGrid
    }

    public void SetGrid(Grid grid)
    {
        _grid = grid;
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        // Redraw every frame in case the grid has changed
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_grid == null) return;

        foreach (var cell in _grid.GetAllCells())
        {
            // Calculate world position (center of cell)
            Vector2 worldPos = _grid.GridToWorld(cell.Position) + new Vector2(_grid.CellSize / 2, _grid.CellSize / 2);

            // Draw the cell node
            Color cellColor = cell.Walkable ? _walkableColor : _unwalkableColor;
            DrawCircle(worldPos, _nodeRadius, cellColor);

            // Optionally show cost values
            if (_showCosts && cell.Walkable)
            {
                DrawString(ThemeDB.FallbackFont, worldPos + new Vector2(0, -8),
                    $"{cell.Cost:F1}", HorizontalAlignment.Center, -1, 8 , _textColor);
            }

            // Draw connections to neighbors
            if (_showConnections)
            {
                foreach (var neighbor in cell.Neighbors.Keys)
                {
                    // Only draw connection one way to avoid duplicates
                    if (cell.Position.X > neighbor.Position.X ||
                        (cell.Position.X == neighbor.Position.X && cell.Position.Y > neighbor.Position.Y))
                        continue;

                    Vector2 neighborPos = _grid.GridToWorld(neighbor.Position) +
                                          new Vector2(_grid.CellSize / 2, _grid.CellSize / 2);

                    DrawLine(worldPos, neighborPos, _connectionColor, _connectionWidth);
                }
            }
        }
    }
}
