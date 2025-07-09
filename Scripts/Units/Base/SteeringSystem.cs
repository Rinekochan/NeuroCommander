using Godot;
using System;
using System.Collections.Generic;
using Godot.Collections;
using NeuroWarCommander.Scripts.Utils;

namespace NeuroWarCommander.Scripts.Units.Base;

public partial class SteeringSystem : Node2D
{
	[Export] public NodePath UnitPath { get; set; }
	[Export] public float MaxSpeed { get; set; } = 100.0f;
	[Export] public float MaxForce { get; set; } = 10.0f;
	[Export] public float SlowingDistance { get; set; } = 100.0f;
	[Export] public float WanderRadius { get; set; } = 50.0f;
	[Export] public float WanderDistance { get; set; } = 80.0f;
	[Export] public float WanderJitter { get; set; } = 10.0f;

	[Export] public float FeelerLength { get; set; } = 90.0f;
	[Export] public int NumFeelers { get; set; } = 5;
	[Export] public float DetectionBoxWidth { get; set; } = 90.0f;

	[Export] public bool RotateToFaceMovement { get; set; } = true;
	[Export] public float RotationSpeed { get; set; } = 5.0f;


	[Export] public SteeringMode CurrentMode = SteeringMode.None;

	public enum SteeringMode
	{
		None,
		Seek,
		Arrive,
		Flee,
		Wander,
		FollowPath
	}

	private UnitBase _unit;
	private Vector2 _velocity = Vector2.Zero;
	private Node2D _boxDetector;
	private Node2D _feelerDetector;

	private Vector2 _lastPosition = Vector2.Zero;
	private float _stuckTime = 0f;
	private float _stuckThreshold = 3.0f; // Time in seconds to consider a unit "stuck"
	private float _minMovementThreshold = 15f; // Minimum distance unit should move in _stuckThreshold time
	private float _backupTimer = 0f;
	private float _backupDuration = 2.0f; // How long to back up when stuck
	private bool _isBackingUp = false;
	private Vector2 _backupDirection = Vector2.Zero;
	private float _randomDirectionTimer = 0f;
	private float _randomDirectionDuration = 2f;

	private Vector2 _wanderTarget;

	private List<Vector2> _path = [];
	private int _currentPathIndex;

	private Vector2 _targetPosition = Vector2.Zero;
	private Node2D _targetEntity;
	private Map _mapRef;

	public void SetMapReference(Map map)
	{
		_mapRef = map;
	}

	public override void _Ready()
	{
		_unit = GetParent<UnitBase>();

		CreateDetectors();

		_wanderTarget = new Vector2(
			(float)GD.RandRange(-1.0, 1.0),
			(float)GD.RandRange(-1.0, 1.0)
			).Normalized() * WanderRadius;

		GD.Print("SteeringSystem: Ready");
	}

	private void CreateDetectors()
	{
		// Create box detector node
		_boxDetector = new Node2D();
		_boxDetector.Name = "BoxDetector";
		AddChild(_boxDetector);

		// Create feeler detector node
		_feelerDetector = new Node2D();
		_feelerDetector.Name = "FeelerDetector";
		AddChild(_feelerDetector);
	}

	public override void _Process(double delta)
	{
		QueueRedraw();
	}

	public override void _PhysicsProcess(double delta)
	{
		if (CurrentMode == SteeringMode.None) return;

		Vector2 steeringForce = Vector2.Zero;
		switch (CurrentMode)
		{
			case SteeringMode.Seek:
				steeringForce = SeekForce(_targetEntity != null ? _targetEntity.GlobalPosition : _targetPosition);
				break;

			case SteeringMode.Arrive:
				steeringForce = ArriveForce(_targetEntity != null ? _targetEntity.GlobalPosition : _targetPosition);
				break;

			case SteeringMode.Flee:
				steeringForce = FleeForce(_targetEntity != null ? _targetEntity.GlobalPosition : _targetPosition);
				break;

			case SteeringMode.Wander:
				steeringForce = WanderForce();
				break;

			case SteeringMode.FollowPath:
				steeringForce = FollowPathForce();
				break;
		}
		steeringForce = steeringForce.LimitLength(MaxForce);

		// Apply obstacle avoidance
		Vector2 avoidanceForce = ObstacleAvoidanceForce();

		if (avoidanceForce != Vector2.Zero)
		{
			steeringForce = avoidanceForce * 0.5f;
		}

		// Check for stuck detection and resolution
		Vector2 stuckResolutionForce = ApplyStuckDetectionAndResolution(delta);
		if (stuckResolutionForce != Vector2.Zero)
		{
			steeringForce = stuckResolutionForce;
		}

		steeringForce = steeringForce.LimitLength(MaxForce);
		_velocity += steeringForce;
		_velocity = _velocity.LimitLength(MaxSpeed);

		// Apply terrain effects if map reference exists
		if (_mapRef != null)
		{
			_mapRef.ApplyTerrainEffect((Vector2I)_unit.GlobalPosition, ref _velocity);
		}

		_unit.Velocity = _velocity;

		// Rotate to face movement direction
		if (RotateToFaceMovement && _velocity.LengthSquared() > 0.1f)
		{
			float targetAngle = _velocity.Angle();
			float currentAngle = _unit.Rotation + Mathf.Pi / 2;

			float angleDiff = Mathf.AngleDifference(targetAngle, currentAngle);

			float rotationAmount = Mathf.Sign(angleDiff) * Mathf.Min(RotationSpeed * (float)delta, Mathf.Abs(angleDiff));
			_unit.Rotation += rotationAmount;
		}
	}

	public void Seek(Vector2 targetPosition)
	{
		CurrentMode = SteeringMode.Seek;
		_targetPosition = targetPosition;
		_targetEntity = null;
	}

	public void SeekEntity(Node2D entity)
	{
		CurrentMode = SteeringMode.Seek;
		_targetEntity = entity;
	}

	public void Arrive(Vector2 targetPosition)
	{
		CurrentMode = SteeringMode.Arrive;
		_targetPosition = targetPosition;
		_targetEntity = null;
	}

	public void ArriveAtEntity(Node2D entity)
	{
		CurrentMode = SteeringMode.Arrive;
		_targetEntity = entity;
	}

	public void Flee(Vector2 targetPosition)
	{
		CurrentMode = SteeringMode.Flee;
		_targetPosition = targetPosition;
		_targetEntity = null;
	}

	public void FleeFromEntity(Node2D entity)
	{
		CurrentMode = SteeringMode.Flee;
		_targetEntity = entity;
	}

	public void Wander()
	{
		CurrentMode = SteeringMode.Wander;
	}

	public void FollowPath(List<Vector2> path)
	{
		_path = path;
		_currentPathIndex = 0;
		CurrentMode = SteeringMode.FollowPath;
	}

	public void Stop()
	{
		CurrentMode = SteeringMode.None;
		_velocity = Vector2.Zero;
		_unit.Velocity = Vector2.Zero;
	}

	private Vector2 SeekForce(Vector2 targetPosition)
    {
        Vector2 desired = targetPosition - _unit.GlobalPosition;
        desired = desired.Normalized() * MaxSpeed;
        return desired - _velocity;
    }

    private Vector2 ArriveForce(Vector2 targetPosition)
    {
        Vector2 toTarget = targetPosition - _unit.GlobalPosition;
        float distance = toTarget.Length();

        if (distance < 10.0f) // Very close, stop moving
            return -_velocity;

        float speed;
        if (distance < SlowingDistance)
        {
            // Inside slowing area, reduce speed
            speed = MaxSpeed * (distance / SlowingDistance);
        }
        else
        {
            // Outside slowing area, go at max speed
            speed = MaxSpeed;
        }

        Vector2 desired = toTarget.Normalized() * speed;
        return desired - _velocity;
    }

    private Vector2 FleeForce(Vector2 targetPosition)
    {
        Vector2 desired = _unit.GlobalPosition - targetPosition;
        desired = desired.Normalized() * MaxSpeed;
        return desired - _velocity;
    }

    private Vector2 WanderForce()
    {
        // Randomly adjust wander target
        _wanderTarget += new Vector2(
            (float)GD.RandRange(-1.0, 1.0) * WanderJitter,
            (float)GD.RandRange(-1.0, 1.0) * WanderJitter
        );

        _wanderTarget = _wanderTarget.Normalized() * WanderRadius;

        // Calculate target in world space
        Vector2 targetLocal = _wanderTarget + new Vector2(WanderDistance, 0);
        Vector2 targetWorld = _unit.GlobalPosition + _unit.Transform.BasisXform(targetLocal);

        return SeekForce(targetWorld);
    }

    private Vector2 FollowPathForce()
    {
        if (_path.Count == 0)
            return Vector2.Zero;

        // Check if we've reached the current waypoint
        Vector2 currentWaypoint = _path[_currentPathIndex];
        float distToWaypoint = _unit.GlobalPosition.DistanceTo(currentWaypoint);

        if (distToWaypoint < 10.0f)
        {
            _currentPathIndex++;

            // Stop if we've reached the end of the path
            if (_currentPathIndex >= _path.Count)
            {
				_velocity = Vector2.Zero;
				CurrentMode = SteeringMode.None;

	            return -_velocity;
            }
        }

        return SeekForce(currentWaypoint);
    }

    private Vector2 ObstacleAvoidanceForce()
    {
	    if (_velocity.LengthSquared() < 0.01f) return Vector2.Zero;

	    // Calculate detection box dimensions based on current speed
	    float speedRatio = _velocity.Length() / MaxSpeed;
	    float detectionLength = FeelerLength * (0.5f + 0.8f * speedRatio);

	    Vector2 facingDirection = Vector2.Up; // In local space, this is forward
	    Vector2 sideVector = Vector2.Right; // In local space, this is right

	    // Check if any obstacles or units intersect with the detection box
	    bool boxObstacleDetected = false;
	    Vector2 boxAvoidanceForce = Vector2.Zero;

	    var spaceState = GetWorld2D().DirectSpaceState;

	    // Check box intersection using shape cast in world space
	    var shape = new RectangleShape2D();
	    shape.Size = new Vector2(detectionLength, DetectionBoxWidth);

	    // Position the box in front of the unit using the box detector's global transform
	    var boxTransform = _boxDetector.GlobalTransform;
	    boxTransform.Origin += boxTransform.Y * detectionLength * 0.5f;

	    var params2D = new PhysicsShapeQueryParameters2D
	    {
		    Shape = shape,
		    Transform = boxTransform,
		    CollideWithBodies = true,
		    CollisionMask = (1 << 1) | (1 << 2), // Unit and Obstacle layer
		    Exclude = [_unit.GetRid()]
	    };

	    var boxResults = spaceState.IntersectShape(params2D);

	    if (boxResults.Count > 0)
	    {
		    boxObstacleDetected = true;
		    // Calculate steering direction based on where in the box the collision occurred
		    var closestDist = float.MaxValue;
		    Vector2 closestPoint = Vector2.Zero;

		    foreach (var result in boxResults)
		    {
			    if (result != null)
			    {
				    // For shape intersections, we need to use collider/shape position
				    if (result.TryGetValue("collider", out var colliderVariant))
				    {
					    Node2D colliderNode = colliderVariant.As<Node2D>();
					    if (colliderNode != null)
					    {
						    Vector2 colliderPos = colliderNode.GlobalPosition;
						    float dist = _unit.GlobalPosition.DistanceTo(colliderPos);
						    if (dist < closestDist)
						    {
							    closestDist = dist;
							    closestPoint = colliderPos;
						    }
					    }
				    }
			    }
		    }

		    // Determine if obstacle is more to the left or right
		    var toObstacle = closestPoint - _unit.GlobalPosition;
		    bool obstacleOnRight = toObstacle.Dot(sideVector) < 0;

		    // Steer away from the obstacle
		    boxAvoidanceForce = obstacleOnRight ? sideVector : -sideVector;
		    boxAvoidanceForce = boxAvoidanceForce.Normalized() * MaxForce * 0.5f;
	    }

	    Vector2 feelerAvoidanceForce = Vector2.Zero;
	    bool feelerObstacleDetected = false;
	    float closestHitDistance = float.MaxValue;
	    Vector2 closestHitNormal = Vector2.Zero;
	    int closestFeelerIdx = -1;

	    // Create feelers from the feeler detector's position
	    var feelers = new List<(Vector2 start, Vector2 end)>();

	    // Main feeler straight ahead
	    feelers.Add((
		    _feelerDetector.GlobalPosition,
		    _feelerDetector.GlobalPosition + _feelerDetector.GlobalTransform.Y * detectionLength * 0.6f
	    ));

	    // Side feelers
	    for (int i = 1; i < NumFeelers; i++)
	    {
		    float angle = 0;
		    if (i % 2 == 1)
			    angle = 0.5f * ((i + 1) / 2); // Right feelers
		    else
			    angle = -0.5f * (i / 2); // Left feelers

		    Vector2 feelerDir = _feelerDetector.GlobalTransform.Y.Rotated(angle);
		    float feelerLength = detectionLength * 0.8f;
		    feelers.Add((
			    _feelerDetector.GlobalPosition,
			    _feelerDetector.GlobalPosition + feelerDir * feelerLength
		    ));
	    }

	    // Check collisions for each feeler
	    for (int i = 0; i < feelers.Count; i++)
	    {
		    var query = new PhysicsRayQueryParameters2D
		    {
			    From = feelers[i].start,
			    To = feelers[i].end,
			    CollideWithBodies = true,
			    CollisionMask = (1 << 1) | (1 << 2), // Unit and Obstacle layer
			    Exclude = [_unit.GetRid()]
		    };

		    var result = spaceState.IntersectRay(query);
		    if (result.Count > 0)
		    {
			    Vector2 hitPos = (Vector2)result["position"];
			    Vector2 normal = (Vector2)result["normal"];
			    float distToObstacle = _unit.GlobalPosition.DistanceTo(hitPos);

				// Record closest hit
				if (distToObstacle < closestHitDistance)
				{
					closestHitDistance = distToObstacle;
					closestHitNormal = normal;
					closestFeelerIdx = i;
				}

				feelerObstacleDetected = true;
			}
		}

		// If a feeler detected an obstacle, calculate avoidance force
		if (feelerObstacleDetected)
		{
			// Calculate penetration amount (how close we are to the obstacle)
			float penetration = detectionLength - closestHitDistance;
			penetration = Mathf.Clamp(penetration / detectionLength, 0.1f, 1.0f);

			// Calculate steering direction based on which feeler was hit
			Vector2 steerDirection;

			if (closestFeelerIdx == 0) // Center feeler hit
			{
				// Choose the best side to steer around obstacle
				if (closestHitNormal.Dot(sideVector) > 0)
					steerDirection = -sideVector; // Steer right
				else
					steerDirection = sideVector;  // Steer left
			}
			else if (closestFeelerIdx % 2 == 1) // Right feelers
			{
				steerDirection = sideVector; // Steer left
			}
			else // Left feelers
			{
				steerDirection = -sideVector; // Steer right
			}

			// Add some direction to avoid dumb steering
			steerDirection = (steerDirection + closestHitNormal * 0.5f).Normalized();
			feelerAvoidanceForce = steerDirection * MaxForce * penetration * 0.5f;
		}

		// Select feeler detection for more precise steering, select box detection in case the feelers miss it.
		Vector2 finalAvoidanceForce = Vector2.Zero;

		if (feelerObstacleDetected)
		{
			finalAvoidanceForce = feelerAvoidanceForce;
		}
		else if (boxObstacleDetected)
		{
			finalAvoidanceForce = boxAvoidanceForce;
		}

		return finalAvoidanceForce;
	}

    private Vector2 ApplyStuckDetectionAndResolution(double delta)
	{
		// Check if we're moving but not making progress
		float distanceMoved = _unit.GlobalPosition.DistanceTo(_lastPosition);

		if (distanceMoved < _minMovementThreshold)
		{
			_stuckTime += (float)delta;
		}
		else
		{
			_stuckTime = 0f;
			_isBackingUp = false;
			_lastPosition = _unit.GlobalPosition;
		}

		// If we're already backing up, continue until the timer expires
		if (_isBackingUp)
		{
			_backupTimer -= (float)delta;
			if (_backupTimer <= 0)
			{
				_isBackingUp = false;
			}
			else
			{
				Vector2 oppositeDir = -_velocity.Normalized();
				_backupDirection = oppositeDir.Rotated((float)GD.RandRange(-Mathf.Pi / 4, Mathf.Pi / 4));
				// Continue backing up
				return _backupDirection * MaxForce * 5.0f;
			}
		}

		// If we're already backing up, continue until the timer expires
		if (_isBackingUp)
		{
			_backupTimer -= (float)delta;
			if (_backupTimer <= 0)
			{
				_isBackingUp = false;

				_randomDirectionTimer = (float)GD.RandRange(0.6f, 1.0f);
				_randomDirectionDuration = _randomDirectionTimer;
				_backupDirection = -_velocity.Normalized();
			}
			else
			{
				// Continue backing up
				return _backupDirection * MaxForce * 5.0f;
			}
		}

		if (_randomDirectionTimer > 0)
		{
			_randomDirectionTimer -= (float)delta;
			float strengthRatio = _randomDirectionTimer / _randomDirectionDuration;
			return _backupDirection * MaxForce * strengthRatio * 5.0f;
		}

		// If we're stuck, initiate backup procedure
		if (_stuckTime > _stuckThreshold)
		{
			if (_unit.IsDebugging)
				GD.Print($"{_unit.Name} is stuck! Initiating backup maneuver.");

			_isBackingUp = true;
			_stuckTime = 0f;
			_backupTimer = _backupDuration;

			// Calculate backup direction - opposite of current movement direction
			Vector2 oppositeDir = -_velocity.Normalized();

			return oppositeDir * MaxForce * 2.0f;
		}

		return Vector2.Zero;
	}

	public override void _Draw()
	{
		if (!_unit.IsDebugging || !_unit.ShowSteeringSystemVisualisation) return;
		// Calculate detection box dimensions based on current speed
		float speedRatio = _velocity.Length() / MaxSpeed;
		float detectionLength = FeelerLength * (0.5f + 0.8f * speedRatio);
		float halfWidth = DetectionBoxWidth / 2.0f;

		// In local space, forward is up (Y+) and right is X+
		Vector2 facingDirection = Vector2.Up;
		Vector2 sideVector = Vector2.Right;

		// Draw detection box
		Color boxColor = new Color(1, 1, 0, 0.5f); // Yellow

		// Calculate the corners in local space
		Vector2 center = facingDirection * detectionLength * 0.5f;
		Vector2 extentX = facingDirection * detectionLength * 0.5f;
		Vector2 extentY = sideVector * halfWidth;

		Vector2 frontLeft = center + extentX + extentY;
		Vector2 frontRight = center + extentX - extentY;
		Vector2 rearLeft = center - extentX + extentY;
		Vector2 rearRight = center - extentX - extentY;

		DrawLine(rearLeft, frontLeft, boxColor);
		DrawLine(frontLeft, frontRight, boxColor);
		DrawLine(frontRight, rearRight, boxColor);
		DrawLine(rearRight, rearLeft, boxColor);

		// Draw feelers
		Color feelerColor = new Color(1, 0.5f, 0, 0.7f); // Orange

		// Center feeler
		DrawLine(Vector2.Zero, facingDirection * detectionLength * 1.2f, feelerColor);

		// Side feelers
		for (int i = 1; i < NumFeelers; i++)
		{
			float angle = 0;
			if (i % 2 == 1)
				angle = 0.5f * ((i + 1) / 2); // Right feelers
			else
				angle = -0.5f * (i / 2); // Left feelers

			Vector2 feelerDir = facingDirection.Rotated(angle);
			float feelerLength = detectionLength * 0.8f;
			DrawLine(Vector2.Zero, feelerDir * feelerLength, feelerColor);
		}

		// Show if unit is backing up or using random direction
		if (_isBackingUp || _randomDirectionTimer > 0)
		{
			Color backupColor = new Color(1, 0, 0, 0.7f); // Red for backup

			DrawLine(Vector2.Zero, _backupDirection * 50f, backupColor, 2.0f);
			DrawCircle(Vector2.Zero, 10f, backupColor);
		}
	}
}
