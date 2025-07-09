using Godot;
using System;
using System.Collections.Generic;

namespace NeuroWarCommander.Scripts.Utils;

public partial class Map : Node2D
{
    [Export] private NodePath _gridNodePath;
    [Export] private NodePath _tileMapPath;
    [Export] private NodePath _mapOverlayPath;
    [Export] private bool _showDebugOverlay = false;

    private Grid _grid;
    private TileMapLayer _tileMap;
    private MapOverlay _mapOverlay;

    // Terrain cost multipliers
    private const float NORMAL_COST = 1.0f;     // Granite, Bridge
    private const float GRAVEL_COST = 1.2f;     // Gravel
    private const float MOUNTAIN_COST = 2.0f;   // Mountain
    private const float UNWALKABLE = -1.0f;     // Water

    // Dictionary mapping tile IDs to movement cost multipliers
    private Dictionary<int, float> _terrainCosts = new()
    {
        { 0, NORMAL_COST },     // Granite
        { 1, MOUNTAIN_COST },   // Mountain
        { 2, NORMAL_COST },     // Bridge
        { 3, NORMAL_COST },     // Bridge
        { 4, GRAVEL_COST },     // Gravel
        { 5, MOUNTAIN_COST },   // Mountain
        { 6, UNWALKABLE },      // Water (unwalkable)
        { 7, UNWALKABLE }       // Water (unwalkable)
    };

    public override void _Ready()
    {
        _grid = GetNode<Grid>(_gridNodePath);
        _tileMap = GetNode<TileMapLayer>(_tileMapPath);
        _mapOverlay = GetNode<MapOverlay>(_mapOverlayPath);

        if (_grid == null || _tileMap == null)
        {
            GD.PrintErr("Map: Required nodes not found! Check the NodePath exports.");
            return;
        }

        GenerateGridFromTileMap();

        // Configure debug overlay if enabled
        if (_mapOverlay != null)
        {
            _mapOverlay.SetGrid(_grid);
            _mapOverlay.Visible = _showDebugOverlay;
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey eventKey && eventKey.Pressed)
        {
            if (eventKey.Keycode == Key.G)
            {
                // Toggle debug overlay visibility
                _showDebugOverlay = !_showDebugOverlay;
                if (_mapOverlay != null)
                {
                    _mapOverlay.Visible = _showDebugOverlay;
                }
                GD.Print($"Debug overlay visibility: {_showDebugOverlay}");
            }
            else if (eventKey.Keycode == Key.R)
            {
                // Regenerate grid from tilemap
                GenerateGridFromTileMap();
            }
        }
    }


    // Get terrain speed multiplier for steering system
    public float GetTerrainSpeedMultiplier(Vector2 worldPos)
    {
        Vector2I tilePos = _tileMap.LocalToMap(worldPos);

        int tileId = _tileMap.GetCellSourceId(tilePos);

        if (_terrainCosts.TryGetValue(tileId, out float cost))
        {
            if (cost <= 0)
                return 0.4f; // Unwalkable terrain, if the unit accidentally steps on it, reduce speed significantly

            return 1.0f / cost;
        }

        return 1.0f;
    }

    // Apply terrain effects to a unit's velocity
    public void ApplyTerrainEffect(Vector2 position, ref Vector2 velocity)
    {
        float speedMultiplier = GetTerrainSpeedMultiplier(position);
        velocity *= speedMultiplier;
    }

    public int GetTerrainType(Vector2 worldPos)
    {
        Vector2I tilePos = _tileMap.LocalToMap(worldPos);
        return _tileMap.GetCellSourceId(tilePos);
    }

    private void GenerateGridFromTileMap()
    {
        GD.Print("Generating grid from tilemap...");

        // Clear the existing grid
        _grid.Clear();

        Rect2I usedRect = _tileMap.GetUsedRect();

        Rect2 gridRect = new Rect2(
            usedRect.Position.X,
            usedRect.Position.Y,
            usedRect.Size.X,
            usedRect.Size.Y
        );

        // Build a basic grid with all cells and connections
        _grid.BuildGrid(gridRect, true);

        var spaceState = GetWorld2D().DirectSpaceState;

        // Update cell costs and walkability based on tile types
        for (int x = 0; x < gridRect.Size.X; x++)
        {
            for (int y = 0; y < gridRect.Size.Y; y++)
            {
                Vector2I tilePos = new Vector2I((int)gridRect.Position.X + x, (int)gridRect.Position.Y + y);
                Vector2 gridPos = new Vector2(tilePos.X, tilePos.Y);
                Vector2 worldPos = _grid.GridToWorld(gridPos);


                TileData tileData = _tileMap.GetCellTileData(tilePos);
                if (tileData != null)
                {
                    int tileId = _tileMap.GetCellSourceId(tilePos);

                    // Use the mapping to determine cost and walkability
                    if (_terrainCosts.TryGetValue(tileId, out float cost))
                    {
                        Grid.GridCell cell = _grid.GetCell(gridPos);
                        if (cell == null) continue;

                        // Check for obstacles in the tile's world position
                        if (CheckForObstacle(worldPos, spaceState))
                        {
                            cell.Cost = cost;
                            cell.Walkable = false;
                            foreach (var keyValue in cell.Neighbors)
                            {
                                var neighbor = keyValue.Key;
                                neighbor.RemoveNeighbor(cell);
                            }
                            cell.Neighbors.Clear();
                            continue;
                        }

                        cell.Cost = _grid.CellSize * cost * cost * cost;
                        cell.Walkable = cost > 0;
                        foreach (var keyValue in cell.Neighbors)
                        {
                            var neighbor = keyValue.Key;
                            // Update neighbor cost based on the current cell's cost
                            float neighborCost = _grid.CalculateMovementCost(cell, neighbor);
                            cell.Neighbors[neighbor] = neighborCost;
                            neighbor.Neighbors[cell] = neighborCost;
                        }
                    }
                }
            }
        }

        GD.Print($"Grid generation complete with {_grid.GetAllCells().Count} cells");
    }

    public Grid GetGrid() => _grid;

    private bool CheckForObstacle(Vector2 worldPos, PhysicsDirectSpaceState2D spaceState)
    {
        // Use a small shape to test for obstacles
        var shape = new RectangleShape2D();
        shape.Size = new Vector2(_grid.CellSize, _grid.CellSize);

        var params2D = new PhysicsShapeQueryParameters2D();
        params2D.Shape = shape;
        params2D.Transform = new Transform2D(0, worldPos + shape.Size / 2);
        params2D.CollisionMask = (1 << 2);
        params2D.CollideWithBodies = true;

        var result = spaceState.IntersectShape(params2D);
        return result.Count > 0;
    }
}
