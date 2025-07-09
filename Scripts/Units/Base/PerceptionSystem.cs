using Godot;
using System;
using System.Collections.Generic;
using NeuroWarCommander.Scripts.Camps;
using NeuroWarCommander.Scripts.Team.Blackboard;

namespace NeuroWarCommander.Scripts.Units.Base;

public partial class PerceptionSystem : Area2D
{
	[Export] public NodePath UnitPath { get; set; }
	// Make it bigger to avoid lag
	[Export] public float DetectionInverval { get; set; } = 2.0f;

	// ------------------------------------------------------------------------
	// Signals for detection events
	[Signal] public delegate void EnemyDetectedEventHandler(UnitBase enemy);
	[Signal] public delegate void EnemyLostFocusedEventHandler(UnitBase friendly);
	[Signal] public delegate void CampDetectedEventHandler(CampBase friendly);
	[Signal] public delegate void CampLostFocusedEventHandler(CampBase friendly);
	[Signal] public delegate void ObstacleDetectedEventHandler(Node2D obstacle);
	[Signal] public delegate void ObstacleLostFocusedEventHandler(Node2D obstacle);

	// ------------------------------------------------------------------------
	// Signals for vision and current unit location
	[Signal] public delegate void VisionUpdateEventHandler(Vector2[] cells);
	[Signal] public delegate void UnitPositionChangedEventHandler(UnitBase itself);

	private UnitBase _unit;
	private HashSet<UnitBase> _detectedAllies = [];
	private HashSet<UnitBase> _detectedEnemies = [];
	private HashSet<CampBase> _detectedCamps = [];
	private HashSet<Node2D> _detectedObstacles = [];

	private double _timeSinceLastDetection;
	private Vector2I _lastUpdatedPosition; // We only send update position signal if the position is changed
	private bool _isVisionCircle;

	public override void _Ready()
	{
		_unit = GetNode<UnitBase>(UnitPath) ?? GetParent<UnitBase>();

		_isVisionCircle = Name == "VisionCircle";

		// Connect area signals
		AreaEntered += OnAreaEntered;
		AreaExited += OnAreaExited;
		BodyEntered += OnBodyEntered;
		BodyExited += OnBodyExited;

		QueueRedraw();

		GD.Print("PerceptionSystem: Ready");
	}

	public override void _Process(double delta)
	{
		_timeSinceLastDetection += delta;

		if (_timeSinceLastDetection >= DetectionInverval)
		{
			_timeSinceLastDetection = 0.0;
			DetectionInverval = 0.5f + GD.Randf() * 0.75f; // Randomize the next detection interval (to avoid lagging at once)
			ScanForEntities();

			RevealFogOfWar();
		}

		QueueRedraw();
	}

	private void ScanForEntities()
	{
		// Get all entities in the area
		var overlappingBodies = GetOverlappingBodies();
		var overlappingAreas = GetOverlappingAreas();

		foreach (var body in overlappingBodies)
		{
			if (body.IsInGroup("fov_cone") || body.IsInGroup("vision_circle"))
			{
				continue; // Skip FOV cone and vision circle themselves
			}

			if (body is Node2D entity)
			{
				ProcessDetection(entity);
			}
		}

		foreach (var area in overlappingAreas)
		{
			if (area.IsInGroup("fov_cone") || area.IsInGroup("vision_circle"))
			{
				continue; // Skip FOV cone and vision circle themselves
			}
			if (area is Node2D entity)
			{
				ProcessDetection(entity);
			}
		}

		if (_isVisionCircle)
		{
			foreach (var enemy in _detectedEnemies)
			{
				if (!CanSeeEntity(enemy))
				{
					ProcessLostDetection(enemy);
				}
			}

			foreach (var friendly in _detectedAllies)
			{
				if (!CanSeeEntity(friendly))
				{
					ProcessLostDetection(friendly);
				}
			}

			foreach (var camp in _detectedCamps)
			{
				if (!CanSeeEntity(camp))
				{
					ProcessLostDetection(camp);
				}
			}
		}
	}

	private void RevealFogOfWar()
	{
		//    We want to reveal cells inside the “VisionCircle” and "FOVCone" area.
        //    We will compute which grid cells (Vector2) are inside,
        //    and then emit them on the "VisionUpdate" signal.

        Vector2 worldPos = _unit.GlobalPosition;

        if ((Vector2I)worldPos != _lastUpdatedPosition) // Also send position update signal if position changed
		{
			_lastUpdatedPosition = (Vector2I)worldPos;
			EmitSignal(SignalName.UnitPositionChanged, _unit);
		}

        // We hardcode the cell size here since we don't have time to make it fancy.
		float cellSize = 16.0f;

        var seenCells = new List<Vector2>();

        if (_isVisionCircle)
        {
            var csNode = GetNodeOrNull<CollisionShape2D>("VisionCircleCollision");
            if (csNode is not { Shape: CircleShape2D circleShape })
                return;

            float visionRadius = circleShape.Radius * _unit.Scale.X + 1;

            // Which grid cell is the unit standing on? (It doesn't know the world, it assumes this cell)
            Vector2 centerCell = new Vector2(
                Mathf.FloorToInt(worldPos.X / cellSize),
                Mathf.FloorToInt(worldPos.Y / cellSize)
            );


            int radiusInCells = Mathf.CeilToInt(visionRadius / cellSize);

            for (int dx = -radiusInCells; dx <= radiusInCells; dx++)
            {
                for (int dy = -radiusInCells; dy <= radiusInCells; dy++)
                {
                    Vector2 cell = new Vector2(centerCell.X + dx, centerCell.Y + dy);

                    // Compute cellCenter in world space
                    Vector2 cellWorldOrigin = cell * cellSize;
                    Vector2 cellCenter = cellWorldOrigin + new Vector2(cellSize, cellSize) * 0.5f;

                    if (worldPos.DistanceTo(cellCenter) <= visionRadius)
                        seenCells.Add(cell);
                }
            }
        }
        else
        {
            var coneNode = GetNodeOrNull<CollisionPolygon2D>("FOVConeCollision");
            if (coneNode == null)
                return;

            // We need the polygon’s points in world space:
            Vector2[] localPoly = coneNode.Polygon;
            var globalTransform = coneNode.GetGlobalTransform();
            Vector2[] worldPoly = globalTransform * localPoly;

            // Determine the bounding‐box of worldPoly
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            foreach (var p in worldPoly)
            {
                if (p.X < minX) minX = p.X;
                if (p.X > maxX) maxX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.Y > maxY) maxY = p.Y;
            }

            // Convert bounding‐box to grid‐cell range
            int x0 = Mathf.FloorToInt(minX / cellSize);
            int x1 = Mathf.FloorToInt(maxX / cellSize);
            int y0 = Mathf.FloorToInt(minY / cellSize);
            int y1 = Mathf.FloorToInt(maxY / cellSize);

            // Loop through each candidate cell in that rectangle
            for (int cx = x0; cx <= x1; cx++)
            {
                for (int cy = y0; cy <= y1; cy++)
                {
                    Vector2 cell = new Vector2(cx, cy);
                    // Compute the world‐space center of that cell
                    Vector2 cellWorldOrigin = cell * cellSize;
                    Vector2 cellCenter = cellWorldOrigin + new Vector2(cellSize, cellSize) * 0.5f;

                    // Perform quick point-in-polygon test for double checking
                    if (Geometry2D.IsPointInPolygon(cellCenter, worldPoly))
                        seenCells.Add(cell);
                }
            }
        }

        // Emit exactly the cells we found
        if (seenCells.Count > 0)
        {
            EmitSignal(SignalName.VisionUpdate, seenCells.ToArray());
        }
    }

	private void OnAreaEntered(Area2D area) => ProcessDetection(area);
	private void OnAreaExited(Area2D area) => ProcessLostDetection(area);
	private void OnBodyEntered(Node2D body) => ProcessDetection(body);
	private void OnBodyExited(Node2D body) => ProcessLostDetection(body);

	private void ProcessDetection(Node2D entity)
	{
		if (entity.IsInGroup("fov_cone") || entity.IsInGroup("vision_circle"))
		{
			return; // Skip FOV cone and vision circle themselves
		}

		if (entity is UnitBase unit && unit.TeamId != _unit.TeamId)
		{
			if (!_detectedEnemies.Contains(unit))
			{
				_detectedEnemies.Add(unit);
			}
			EmitSignal(SignalName.EnemyDetected, unit);
		}
		else if (entity is UnitBase friendly && friendly.TeamId == _unit.TeamId)
		{
			if (!_detectedAllies.Contains(friendly))
			{
				_detectedAllies.Add(friendly);
			}
		}
		else if (entity is CampBase camp)
		{
			if (_detectedCamps.Add(camp))
			{
				EmitSignal(SignalName.CampDetected, camp);
			}
		}
		else if (entity is StaticBody2D obstacle && obstacle.IsInGroup("obstacles"))
		{
			if (_detectedObstacles.Add(obstacle))
			{
				EmitSignal(SignalName.ObstacleDetected, obstacle);
			}
		}
		else if (entity is Area2D area && area.IsInGroup("obstacles"))
		{
			if (_detectedObstacles.Add(area))
			{
				EmitSignal(SignalName.ObstacleDetected, area);
			}
		}
		else
		{
			// GD.PrintErr("Unknown entity detected: " + entity.Name);
		}
	}

	private void ProcessLostDetection(Node2D entity)
	{
		if (entity is UnitBase unit && _detectedEnemies.Contains(unit))
		{
			_detectedEnemies.Remove(unit);
			EmitSignal(SignalName.EnemyLostFocused, unit);
		}
		else if (entity is UnitBase friendly && _detectedAllies.Contains(friendly))
		{
			_detectedAllies.Remove(friendly);
		}
		else if (entity is CampBase camp && _detectedCamps.Contains(camp))
		{
			_detectedCamps.Remove(camp);
			EmitSignal(SignalName.CampLostFocused, camp);
		}
	}

	public HashSet<UnitBase> GetVisibleAllies()
	{
		return _detectedAllies;
	}

	public HashSet<UnitBase> GetVisibleEnemies()
	{
		return _detectedEnemies;
	}

	public HashSet<CampBase> GetVisibleCamps()
	{
		return _detectedCamps;
	}

	public HashSet<Node2D> GetVisibleObstacles()
	{
		return _detectedObstacles;
	}

	public bool CanSeeEntity(Node2D entity)
	{
		if (entity is UnitBase)
			return _detectedEnemies.Contains((UnitBase)entity);

		if (entity is CampBase)
		{
			return _detectedCamps.Contains((CampBase)entity);
		}

		return false;
	}

	public void DisconnectAllSignals()
	{
		// Check if parent node is still valid before disconnecting
		var parentNode = GetParent();
		if (parentNode == null || !IsInstanceValid(parentNode))
			return;

		// Find the team node to get access to the blackboard
		var teamNode = parentNode;
		while (teamNode != null && !teamNode.HasNode("Blackboard"))
		{
			teamNode = teamNode.GetParent();
		}

		if (teamNode == null)
			return;

		var blackboard = teamNode.GetNode<Blackboard>("Blackboard");
		if (blackboard == null)
			return;

		// Disconnect all signals connected to the blackboard
		Disconnect(nameof(VisionUpdate), new Callable(blackboard, nameof(Blackboard.VisionUpdate)));
		Disconnect(nameof(UnitPositionChanged), new Callable(blackboard, nameof(Blackboard.OnUnitPositionChanged)));
		Disconnect(nameof(EnemyDetected), new Callable(blackboard, nameof(Blackboard.OnEnemyDetected)));
		Disconnect(nameof(EnemyLostFocused), new Callable(blackboard, nameof(Blackboard.OnEnemyLostFocused)));
		Disconnect(nameof(CampDetected), new Callable(blackboard, nameof(Blackboard.OnCampDetected)));
		Disconnect(nameof(CampLostFocused), new Callable(blackboard, nameof(Blackboard.OnCampLostFocused)));
		Disconnect(nameof(ObstacleDetected), new Callable(blackboard, nameof(Blackboard.ObstacleDetected)));
		Disconnect(nameof(ObstacleLostFocused), new Callable(blackboard, nameof(Blackboard.ObstacleLostFocused)));
	}

	public override void _Draw()
	{
		if (!_unit.IsDebugging || !_unit.ShowPerceptionSystemVisualisation) return;

		// Visualization color settings
		Color visionColor = _isVisionCircle
			? new Color(0, 0.5f, 1, 0.2f) // Blue for vision circle (peripheral vision)
			: new Color(1, 0.7f, 0, 0.3f); // Orange for FOV cone (focused vision)

		Color borderColor = visionColor with { A = 0.7f };

		if (_isVisionCircle)
		{
			// Get the Vision Circle node
			var visionCollisionShape = GetNode<CollisionShape2D>("VisionCircleCollision");
			if (visionCollisionShape == null) return;

			var shape = visionCollisionShape.Shape;
			var circleShape = shape as CircleShape2D;
			if (circleShape == null) return;

			// Draw filled circle
			DrawCircle(Vector2.Zero, circleShape.Radius, visionColor);

			// Draw border
			DrawArc(Vector2.Zero, circleShape.Radius, 0, Mathf.Tau, 32, borderColor, 2.0f);

			// Draw direction indicator (forward vector)
			Vector2 forward = new Vector2(circleShape.Radius, 0);
			DrawLine(Vector2.Zero, forward, borderColor, 2.0f);
		}
		else
		{
			// Get the FOV cone Circle node
			var fovShape = GetNode<CollisionPolygon2D>("FOVConeCollision");
			if (fovShape == null) return;

			Vector2[] points = fovShape.Polygon;

			// Draw filled polygon
			DrawColoredPolygon(points, visionColor);

			// Draw polygon border
			for (int i = 0; i < points.Length; i++)
			{
				int nextIdx = (i + 1) % points.Length;
				DrawLine(points[i], points[nextIdx], borderColor, 2.0f);
			}
		}
	}
}
