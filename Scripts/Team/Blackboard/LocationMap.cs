#nullable enable
using Godot;
using System;
using System.Collections.Generic;
using NeuroWarCommander.Scripts.Camps;
using NeuroWarCommander.Scripts.Units.Base;
using NeuroWarCommander.Scripts.Utils;

namespace NeuroWarCommander.Scripts.Team.Blackboard;

// This map mainly tracks the position of obstacles and health of entities like camps and enemy units.
public partial class LocationMap : Node
{
    [Signal] public delegate void NewCampEventHandler(Node2D entity, CampBase type);

    public enum EntityType { Obstacle, AllyCamp, EnemyCamp, NeutralCamp, EnemyUnit, AllyUnit }
    public class Entity // For Obstacles, Camps and Enemy Units
    {
        public Vector2I Position { get; set; }
        public EntityType Type { get; set; }
        public Node2D? EntityNode { get; set; } = null; // Reference to the entity node, if needed
        public bool Focused { get; set; } = false;
        public float MaxHealth { get; set; }
        public float Health { get; set; }

        public Entity(Vector2I position, EntityType type)
        {
            Position = position;
            Type = type;
        }
    }

    [Export] public Vector2I MapSize = new(2048, 2048);
    [Export] public Vector2I MapCenter = Vector2I.Zero;
    [Export] public int CellSize = 16;

    public Dictionary<Vector2I, Entity?> LocationCells { get; private set; } = new();

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

        BuildGrid(_mapBounds); // Build vision grid
    }

    // This will handle obstacles, camps, and enemy units detection signals
    public void UpdateFocused(Node2D entity, EntityType type)
    {
        Vector2I position = (Vector2I)_grid.WorldToGrid(entity.GlobalPosition);

        // If the entity is unit, and it's already there, just update its health, and position
        if (type is EntityType.AllyUnit or EntityType.EnemyUnit)
        {
            // Check if the entity already exists in the map
            foreach (var kvp in LocationCells)
            {
                if (kvp.Value?.EntityNode == entity)
                {
                    Entity unit = kvp.Value; // Get the existing entity
                    // We need to remove the entity in the location cell if the position has changed
                    if (unit.Position != position)
                    {
                        var tempPosition = unit.Position;
                        unit.Position = position;
                        LocationCells[tempPosition] = null;
                        LocationCells[position] = unit; // Update the position in the map
                    }
                    kvp.Value.Health = (float)entity.Get("CurrentHealth");
                    return; // Exit after updating the existing entity
                }
            }

            // If the entity is not found, create a new one
            LocationCells[position] = new Entity(position, type)
            {
                Focused = true,
                EntityNode = entity,
                Health = (float)entity.Get("CurrentHealth"),
                MaxHealth = (float)entity.Get("MaxHealth")
            };
            return;
        }

        // For static entities like obstacles, and camps we can directly update or create them by refering to position
        if (LocationCells.TryGetValue(position, out Entity? e) && e != null)
        {
            // If the entity type changes (from neutral camp to enemy camp, for example), we need to update it
            if (e.Type != type)
            {
                e.Type = type; // Update the type of the entity
                e.EntityNode = entity; // Update the reference to the entity node
            }
            e.Focused = true;
            if (type != EntityType.Obstacle)
            {
                e.Health = (float)entity.Get("CurrentHealth");
            }
        }
        else
        {
            // Create a new entity if it doesn't exist
            LocationCells[position] = new Entity(position, type)
            {
                Focused = true,
                EntityNode = entity
            };

            if (type != EntityType.Obstacle) // If it's not an obstacle, it will be camp base
            {
                LocationCells[position]!.Health = (float)entity.Get("CurrentHealth");
                LocationCells[position]!.MaxHealth = (float)entity.Get("MaxHealth");
                EmitSignal(SignalName.NewCamp, (CampBase)entity);
            }
        }
        GarbageCollect();
    }

    // This will handle obstacles, camps, and enemy units Not Focused signals
    public void UpdateNotFocused(Node2D entity)
    {
        Vector2I position = (Vector2I)_grid.WorldToGrid(entity.GlobalPosition);
        if (LocationCells.TryGetValue(position, out Entity? e) && e != null)
        {
            e.Focused = false;
        }

        // Also do manual garbage collection
        GarbageCollect();
    }

    // This will remove an ally if it's dead
    public void UpdateAllyDead(UnitBase ally, Vector2 worldPos)
    {
        Vector2I position = (Vector2I)_grid.WorldToGrid(worldPos);

        if (LocationCells.TryGetValue(position, out Entity? entity) && entity != null)
        {
            // GD.Print("What do we have here? " + entity.EntityNode?.Name);
            if (entity.Type != EntityType.AllyUnit)
            {
                return;
            }
            LocationCells[position] = null; // Remove the entity from the map
        }

        // Also do manual garbage collection
        GarbageCollect();
    }

    // The Blackboard will use this to remove entities that are no longer there when the cell is updated in vision map
    public void UpdateUnseenEnemies(Vector2I position)
    {
        if (LocationCells.TryGetValue(position, out Entity? entity) && entity != null)
        {
            // If the entity is focused or not enemies, we don't remove it (since obstacles and camps are static, and we should always see Ally Units)
            if (entity.Focused || entity.Type != EntityType.EnemyUnit)
            {
                return;
            }

            LocationCells[position] = null; // Remove the entity from the map
        }

        // Also do manual garbage collection
        GarbageCollect();
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
                LocationCells[pos] = null; // Initialize all cells as null
            }
        }
    }

    public Entity? GetLocationCell(Vector2I gridPosition)
    {
        return LocationCells.GetValueOrDefault(gridPosition, null);
    }

    // Return all detected enemy units, and allies
    public List<Tuple<Vector2I, Entity>> GetAllUnits()
    {
        List<Tuple<Vector2I, Entity>> units = [];
        foreach (var kvp in LocationCells)
        {
            if (kvp.Value is { Type: EntityType.EnemyUnit or EntityType.AllyUnit, EntityNode: UnitBase })
            {
                units.Add(new Tuple<Vector2I, Entity>(kvp.Key, kvp.Value));
            }
        }
        return units;
    }

    // Return all ally camps
    public List<Tuple<Vector2I, Entity>> GetAllAllyCamps()
    {
        List<Tuple<Vector2I, Entity>> allyCamps = [];
        foreach (var kvp in LocationCells)
        {
            if (kvp.Value is { Type: EntityType.AllyCamp, EntityNode: CampBase })
            {
                allyCamps.Add(new Tuple<Vector2I, Entity>(kvp.Key, kvp.Value));
            }
        }
        return allyCamps;
    }

    // Return all enemy camps
    public List<Tuple<Vector2I, Entity>> GetAllEnemyCamps()
    {
        List<Tuple<Vector2I, Entity>> enemyCamps = [];
        foreach (var kvp in LocationCells)
        {
            if (kvp.Value is { Type: EntityType.EnemyCamp, EntityNode: CampBase })
            {
                enemyCamps.Add(new Tuple<Vector2I, Entity>(kvp.Key, kvp.Value));
            }
        }
        return enemyCamps;
    }

    // Return all neutral camps
    public List<Tuple<Vector2I, Entity>> GetAllNeutralCamps()
    {
        List<Tuple<Vector2I, Entity>> neutralCamps = [];
        foreach (var kvp in LocationCells)
        {
            if (kvp.Value is { Type: EntityType.NeutralCamp, EntityNode: CampBase })
            {
                neutralCamps.Add(new Tuple<Vector2I, Entity>(kvp.Key, kvp.Value));
            }
        }
        return neutralCamps;
    }

    private void GarbageCollect()
    {
        // This method is used to remove entities that are no longer valid
        foreach (var kvp in LocationCells)
        {
            if (kvp.Value == null)
            {
                continue; // Skip null entries
            }

            if (!IsInstanceValid(kvp.Value.EntityNode) || !kvp.Value.EntityNode.IsInsideTree())
            {
                LocationCells[kvp.Key] = null;
            }
        }
    }
}
