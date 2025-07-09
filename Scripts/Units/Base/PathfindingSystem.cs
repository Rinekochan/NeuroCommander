using Godot;
using System;
using System.Collections.Generic;
using NeuroWarCommander.Scripts.Utils;

namespace NeuroWarCommander.Scripts.Units.Base;

public partial class PathfindingSystem : Node2D
{
    private class PathNode(Grid.GridCell cell, float gCost = 0, float hCost = 0)
    {
        public Grid.GridCell Cell = cell;
        public float GCost = gCost; // Cost from start
        public float HCost = hCost; // Heuristic cost to end
        public float FCost => GCost + HCost; // Total cost

        public PathNode Parent = null;
    }

    private UnitBase _unit;
    private Grid _grid;
    private List<Vector2> _currentPath = [];
    private Vector2 _lastPathStart;
    private Vector2 _lastPathEnd;

    private static Node2D _pathVisualizer;

    public override void _Ready()
    {
        _unit = GetParent() as UnitBase;
        GD.Print("PathfindingSystem: Ready");

        CallDeferred(nameof(SetupPathVisualizer));
    }

    private void SetupPathVisualizer()
    {
        // Only create the visualizer if it doesn't exist yet
        if (_pathVisualizer == null)
        {
            _pathVisualizer = new Node2D();
            _pathVisualizer.Name = "PathVisualizer" + _unit.Name;

            GetTree().Root.CallDeferred(Node.MethodName.AddChild, _pathVisualizer);
        }
    }

    public void SetGrid(Grid grid)
    {
        _grid = grid;
    }

    // Find the shortest path using A* algorithm
    public List<Vector2> FindPath(Vector2 startWorldPos, Vector2 endWorldPos)
    {
        // Check if we're calculating the same path
        if (startWorldPos == _lastPathStart && endWorldPos == _lastPathEnd && _currentPath.Count > 0)
        {
            return _currentPath;
        }

        // Convert world positions to grid cell positions
        Vector2 startCellPos = _grid.WorldToGrid(startWorldPos);
        Vector2 endCellPos = _grid.WorldToGrid(endWorldPos);

        // Get the grid cells for start and end positions
        Grid.GridCell startCell = _grid.GetCell(startCellPos);
        Grid.GridCell endCell = _grid.GetCell(endCellPos);


        if (startCell == null || endCell == null || !startCell.Walkable || !endCell.Walkable)
        {
            if (endCell is not { Walkable: true })
            {
                Grid.GridCell nearestWalkable = FindNearestWalkableCell(endCellPos);
                if (nearestWalkable != null)
                {
                    endCell = nearestWalkable;
                }
                else
                {
                    return [];
                }
            }

            if (startCell is not { Walkable: true })
            {
                Grid.GridCell nearestWalkable = FindNearestWalkableCell(startCellPos);
                if (nearestWalkable != null)
                {
                    startCell = nearestWalkable;
                }
                else
                {
                    return [];
                }
            }
        }

        // A* algorithm
        var frontier = new PriorityQueue<PathNode, float>();
        var visited = new HashSet<Grid.GridCell>();
        var nodeDictionary = new Dictionary<Grid.GridCell, PathNode>();

        var startNode = new PathNode(startCell)
        {
            HCost = CalculateHeuristic(startCell, endCell)
        };

        frontier.Enqueue(startNode, startNode.FCost);
        nodeDictionary[startCell] = startNode;

        while (frontier.Count > 0)
        {
            var currentNode = frontier.Dequeue();
            if (visited.Contains(currentNode.Cell)) continue;

            visited.Add(currentNode.Cell);

            if (currentNode.Cell == endCell)
            {
                _lastPathStart = startWorldPos;
                _lastPathEnd = endWorldPos;
                _currentPath = ReconstructPath(startNode, currentNode);

                VisualizeCurrentPath();

                return _currentPath;
            }

            // Check all neighbors
            foreach (var neighborPair in currentNode.Cell.Neighbors)
            {
                Grid.GridCell neighbor = neighborPair.Key;
                float moveCost = neighborPair.Value;

                Vector2 neighborPos = GetCellCenterWorld(neighbor);
                if (CheckForUnitCollisions(neighborPos, _grid.CellSize)) moveCost *= 2.0f; // Increase cost if collides with other units

                if (visited.Contains(neighbor) || !neighbor.Walkable) continue;

                float newGCost = currentNode.GCost + moveCost;

                // Check if this is a better path
                if(!nodeDictionary.TryGetValue(neighbor, out var neighborNode) || newGCost < neighborNode.GCost)
                {
                    if (neighborNode == null)
                    {
                        neighborNode = new PathNode(neighbor, newGCost, CalculateHeuristic(neighbor, endCell))
                        {
                            Parent = currentNode
                        };
                    }
                    else
                    {
                        neighborNode.GCost = newGCost;
                        neighborNode.HCost = CalculateHeuristic(neighbor, endCell);
                        neighborNode.Parent = currentNode;
                    }

                    nodeDictionary[neighbor] = neighborNode;

                    frontier.Enqueue(neighborNode, neighborNode.FCost);
                }
            }
        }
        return [];
    }

    private float CalculateHeuristic(Grid.GridCell from, Grid.GridCell to)
    {
        // Base cost is Manhattan distance
        float baseCost = Mathf.Abs(from.Position.X - to.Position.X) +
                         Mathf.Abs(from.Position.Y - to.Position.Y);

        // Add a penalty if current cell is near other units
        Vector2 fromPos = GetCellCenterWorld(from);
        if (CheckForUnitCollisions(fromPos, _grid.CellSize))
        {
            baseCost *= 5.0f;
        }

        // Add a penalty if destination cell is near other units
        Vector2 toPos = GetCellCenterWorld(to);
        if (CheckForUnitCollisions(toPos, _grid.CellSize))
        {
            baseCost *= 20.0f;
        }

        return baseCost;
    }

    private List<Vector2> ReconstructPath(PathNode startNode, PathNode endNode)
    {
        List<Vector2> path = [];
        PathNode current = endNode;

        // Trace backwards
        while (current != null && current != startNode)
        {
            Vector2 cellCenter = GetCellCenterWorld(current.Cell);
            path.Add(cellCenter);
            current = current.Parent;
        }

        path.Reverse();

        // Make the path smoother
        path = SimplifyPath(path);

        return path;
    }

    private Vector2 GetCellCenterWorld(Grid.GridCell cell)
    {
        Vector2 topLeft = _grid.GridToWorld(cell.Position);
        return topLeft + new Vector2(_grid.CellSize / 2, _grid.CellSize / 2);
    }

    private List<Vector2> SimplifyPath(List<Vector2> path)
    {
        if (path.Count <= 2) return path;

        List<Vector2> simplifiedPath = [path[0]];

        int current = 0;
        while (current < path.Count - 1)
        {
            int next = current + 1;

            // Allow at most 4 points to be skipped to avoid bugs :(
            int maxSkip = Math.Min(4, path.Count - current - 1);

            for (int skip = 1; skip <= maxSkip; skip++)
            {
                int testPoint = current + skip;
                if (testPoint >= path.Count) break;

                Vector2 dir = (path[next] - path[current]).Normalized();

                // Make sure we can walk directly between two points, and make sure tight turns are handled
                if (CanWalkDirectly(path[current], path[testPoint]))
                {
                    next = testPoint;
                }
                else
                {
                    break; // Stop if we can't walk directly
                }
            }

            simplifiedPath.Add(path[next]);
            current = next;
        }

        return simplifiedPath;
    }

    private bool CanWalkDirectly(Vector2 start, Vector2 end)
    {
        // Check for direct collisions
        var spaceState = GetWorld2D().DirectSpaceState;
        var query = new PhysicsRayQueryParameters2D
        {
            From = start,
            To = end,
            CollideWithBodies = true,
            CollisionMask = (1 << 1) | (1 << 2), // Units and obstacles layers
            Exclude = [_unit.GetRid()]
        };

        var result = spaceState.IntersectRay(query);
        if (result.Count > 0)
        {
            return false;
        }

        float distance = start.DistanceTo(end);
        int steps = Mathf.Max(20, Mathf.CeilToInt(distance / (_grid.CellSize / 0.25)));

        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;
            Vector2 checkPos = start.Lerp(end, t);

            if (!IsSafePosition(checkPos)) return false; // Check if the position is walkable
        }

        return true;
    }

    private bool CheckForUnitCollisions(Vector2 position, float radius = 10.0f)
    {
        var spaceState = GetWorld2D().DirectSpaceState;

        // Create a circle query shape
        var circleShape = new CircleShape2D();
        circleShape.Radius = radius;
        var query = new PhysicsShapeQueryParameters2D()
        {
            Shape = circleShape,
            Transform = new Transform2D(0, position),
            CollisionMask = 1 << 1,
            Exclude = [_unit.GetRid()] // Exclude self
        };

        // Perform the query
        var results = spaceState.IntersectShape(query);

        return results.Count > 0; // Return true if there are collisions
    }

    private bool IsSafePosition(Vector2 worldPos)
    {
        Vector2 gridPos = _grid.WorldToGrid(worldPos);
        Grid.GridCell cell = _grid.GetCell(gridPos);
        if (cell == null || !cell.Walkable) return false;

        // check if there's a unit collision at this position
        return !CheckForUnitCollisions(worldPos, _grid.CellSize * 0.4f);
    }

    private Grid.GridCell FindNearestWalkableCell(Vector2 endWorldPos)
    {
        int searchRadius = 1;
        int maxSearchRadius = 30;
        while (searchRadius <= maxSearchRadius)
        {
            for (int x = -searchRadius; x <= searchRadius; x++)
            {
                for (int y = -searchRadius; y <= searchRadius; y++)
                {
                    Vector2 offset = new Vector2(x, y);
                    Vector2 checkPos = endWorldPos + offset;
                    Grid.GridCell cell = _grid.GetCell(checkPos);
                    if (cell != null && cell.Walkable)
                    {
                        return cell;
                    }
                }
            }

            searchRadius++;
        }

        return null;
    }

    // Method to visualize the current path
    public void VisualizeCurrentPath()
    {
        // Clear any existing visualization
        ClearPathVisualization();

        if (!_unit.IsDebugging || _pathVisualizer == null || _currentPath.Count < 2 || !_unit.ShowPathfindingSystemVisualisation) return;

        // Create a Line2D for the path
        Line2D pathLine = new Line2D();
        pathLine.Width = 2.0f;
        pathLine.DefaultColor = new Color(0, 1, 0, 0.7f);

        // Add points to the line using global positions directly
        foreach (var point in _currentPath)
        {
            // No conversion needed since _currentPath already contains global positions
            pathLine.AddPoint(point);
        }

        _pathVisualizer.AddChild(pathLine);

        // Add waypoint markers
        for (int i = 0; i < _currentPath.Count; i++)
        {
            Sprite2D waypoint = new Sprite2D();

            // Different appearance for the final destination
            if (i == _currentPath.Count - 1)
            {
                waypoint.Texture = CreateCircleTexture(6, new Color(1, 0, 0, 0.8f));
                waypoint.Scale = Vector2.One * 1.5f;
            }
            else
            {
                waypoint.Texture = CreateCircleTexture(4, new Color(1, 1, 0, 0.7f));
            }

            // Use global position directly
            waypoint.Position = _currentPath[i];
            _pathVisualizer.AddChild(waypoint);
        }
    }

    // Helper to create a circle texture
    private Texture2D CreateCircleTexture(int radius, Color color)
    {
        var image = Image.CreateEmpty(radius * 2, radius * 2, false, Image.Format.Rgba8);
        image.Fill(new Color(0, 0, 0, 0));

        for (int x = 0; x < radius * 2; x++)
        {
            for (int y = 0; y < radius * 2; y++)
            {
                float distance = new Vector2(x - radius, y - radius).Length();
                if (distance <= radius)
                {
                    image.SetPixel(x, y, color);
                }
            }
        }

        return ImageTexture.CreateFromImage(image);
    }

    // Method to clear path visualization
    public void ClearPathVisualization()
    {
        if (_pathVisualizer == null) return;

        foreach (var child in _pathVisualizer.GetChildren())
        {
            child.QueueFree();
        }
    }
}