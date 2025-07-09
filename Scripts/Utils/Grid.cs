using Godot;
using System;
using System.Collections.Generic;

namespace NeuroWarCommander.Scripts.Utils;

public partial class Grid : Node
{
    public class GridCell
    {
        public Vector2 Position { get; private set; }
        public bool Walkable { get; set; } = true;
        public float Cost { get; set; } = 1.0f;
        public Dictionary<GridCell, float> Neighbors { get; private set; } = new();

        public GridCell(Vector2 position)
        {
            Position = position;
        }

        public void AddNeighbor(GridCell neighbor, float cost)
        {
            if (!Neighbors.ContainsKey(neighbor))
            {
                Neighbors[neighbor] = cost;
            }
        }

        public void RemoveNeighbor(GridCell neighbor)
        {
            if (Neighbors.ContainsKey(neighbor))
            {
                Neighbors.Remove(neighbor);
            }
        }

        public void UpdateCost(GridCell neighbor, float cost)
        {
            if (Neighbors.ContainsKey(neighbor))
            {
                Neighbors[neighbor] = cost;
            }
        }
    }

    [Export] public float CellSize = 16.0f;
    public Dictionary<Vector2, GridCell> Cells = new();

    public Vector2 GridToWorld(Vector2 gridPosition)
    {
        return gridPosition * CellSize;
    }

    public Vector2 WorldToGrid(Vector2 worldPosition)
    {
        return new Vector2(
            Mathf.Floor(worldPosition.X / CellSize),
            Mathf.Floor(worldPosition.Y / CellSize)
        );
    }

    public GridCell CreateCell(Vector2 gridPosition)
    {
        if (!Cells.ContainsKey(gridPosition))
        {
            Cells[gridPosition] = new GridCell(gridPosition);
        }
        return Cells[gridPosition];
    }

    public bool RemoveCell(Vector2 gridPosition)
    {
        if (Cells.ContainsKey(gridPosition))
        {
            GridCell cellToRemove = Cells[gridPosition];

            // Remove all neighbors from this cell
            foreach (var neighbor in cellToRemove.Neighbors.Keys)
            {
                neighbor.RemoveNeighbor(cellToRemove);
            }

            return Cells.Remove(gridPosition);
        }
        return false;
    }

    public GridCell GetCell(Vector2 gridPosition)
    {
        Cells.TryGetValue(gridPosition, out GridCell cell);
        return cell;
    }

    public GridCell GetCellFromWorld(Vector2 worldPosition)
    {
        Vector2 gridPosition = WorldToGrid(worldPosition);
        return GetCell(gridPosition);
    }

    public List<GridCell> GetAllCells() => [..Cells.Values];

    public void Connect4Way(Vector2 position)
    {
        if(!Cells.ContainsKey(position)) return;

        GridCell cell = Cells[position];

        // Check and connect to the four adjacent cells
        Vector2[] directions = {
            new(1, 0),  // Right
            new(-1, 0), // Left
            new(0, 1),  // Down
            new(0, -1)  // Up
        };

        foreach (var dir in directions)
        {
            Vector2 neighborPos = position + dir;
            if (Cells.ContainsKey(neighborPos) && Cells[neighborPos].Walkable)
            {
                // Connect both ways
                float cost = CalculateMovementCost(cell, Cells[neighborPos]);
                cell.AddNeighbor(Cells[neighborPos], cost);
                Cells[neighborPos].AddNeighbor(cell, cost);
            }
        }
    }

    public void Connect8Way(Vector2 position)
    {
        if (!Cells.ContainsKey(position))
            return;

        GridCell cell = Cells[position];

        // Check and connect to all eight surrounding cells
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                if (x == 0 && y == 0) continue; // Skip self

                Vector2 neighborPos = position + new Vector2(x, y);
                if (Cells.ContainsKey(neighborPos) && Cells[neighborPos].Walkable)
                {
                    // Connect both ways
                    float cost = CalculateMovementCost(cell, Cells[neighborPos]);
                    cell.AddNeighbor(Cells[neighborPos], cost);
                    Cells[neighborPos].AddNeighbor(cell, cost);
                }
            }
        }
    }

    public void UpdateCellWalkable(Vector2 position, bool walkable)
    {
        if (Cells.ContainsKey(position))
        {
            Cells[position].Walkable = walkable;

            // Update connections - remove connections if not walkable
            if (!walkable)
            {
                foreach (var cell in Cells.Values)
                {
                    cell.RemoveNeighbor(Cells[position]);
                }
                Cells[position].Neighbors.Clear();
            }
            else
            {
                // Re-establish connections
                Connect8Way(position);
            }
        }
    }

    public float CalculateMovementCost(GridCell from, GridCell to)
    {
        float baseCost = from.Position.DistanceTo(to.Position);

        return baseCost * to.Cost;
    }

    public void BuildGrid(Rect2 bounds, bool connected)
    {
        for (int x = 0; x < bounds.Size.X; x++)
        {
            for (int y = 0; y < bounds.Size.Y; y++)
            {
                Vector2 pos = new Vector2(x, y) + bounds.Position;
                CreateCell(pos);
            }
        }

        if (!connected) return; // If we don't want connections, exit early
        foreach (var cell in Cells.Keys)
            Connect8Way(cell);
    }

    public void Clear()
    {
        Cells.Clear();
    }
}
