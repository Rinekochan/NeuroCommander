using Godot;
using System.Linq;
using NeuroWarCommander.Scripts.Camps;
using NeuroWarCommander.Scripts.Team.Blackboard;
using NeuroWarCommander.Scripts.Utils;

namespace NeuroWarCommander.Scripts.Units.Base;

public partial class UnitFSM : Node2D
{

	public enum State
	{
		Idle, // And also defending if any enemies go into the FOV corn.
		Moving,
		Capturing,
		Rotating,
		Dead
	}

	[Signal] public delegate void StateChangedEventHandler(Node2D source, State newState, State oldState);
	[Signal] public delegate void TargetCapturedEventHandler(Node2D source, Node2D target);

	[Export] public State InitialState { get; set; } = State.Idle;

	[Export] public float RotationSpeed { get; set; } = 5.0f;
	[Export] public float AcceptableAngleError { get; set; } = 0.2f;

	protected State CurrentState;
	protected State OldState;
	protected SteeringSystem Steering;
	protected PathfindingSystem Pathfinding;
	protected PerceptionSystem FovCone;
	protected PerceptionSystem VisionCircle;

	protected Node2D CurrentTarget;
	protected CampBase CampToCapture;
	protected float TargetRotation = 0.0f;
	protected bool IsRotating = false;
	protected System.Collections.Generic.List<Vector2> PathToFollow;
	protected Vector2 MoveDestination;

	protected UnitBase Unit;

	private Timer _retreatTimer;

	public override void _Ready()
	{
		// Get references to unit systems
		Unit = GetParent<UnitBase>();
		Steering = Unit.GetNode<SteeringSystem>("Steering");
		Pathfinding = Unit.GetNode<PathfindingSystem>("Pathfinding");
		FovCone = Unit.GetNode<PerceptionSystem>("FOVCone");
		VisionCircle = Unit.GetNode<PerceptionSystem>("VisionCircle");

		// Set camp signals from location map
		var locationMap = GetParent().GetParent().GetParent().GetNode<Blackboard>("Blackboard").GetNode<LocationMap>("LocationMap");

		locationMap.Connect(nameof(LocationMap.NewCamp), new Callable(this, nameof(SetKnownCamps)));
	}

	public void SetKnownCamps(CampBase camp)
	{
		camp.Connect(nameof(CampBase.CampReducedToZeroHP), new Callable(this, nameof(OnCampReducedToZeroHP)));
	}

	// Command APIs for controlling the unit from the AI or player
	public virtual void MoveTo(Vector2 position, State state)
	{
		if (PathToFollow != null && PathToFollow.Count > 0)
		{
			Steering.Stop();
			PathToFollow.Clear();
		}

		CurrentTarget = null;
		var path = Pathfinding.FindPath(Unit.GlobalPosition, position);

		if (path.Count > 0)
		{
			PathToFollow = path;
			MoveDestination = position;

			// Calculate direction to first waypoint
			Vector2 direction = (path[0] - Unit.GlobalPosition).Normalized();

			// Check if we need to rotate first
			if (!IsRotatedToDirection(direction))
			{
				// Rotate to face the path direction first
				OldState = state;
				RotateToDirection(direction);
				TransitionToState(State.Rotating);
			}
			else
			{
				Steering.FollowPath(path);
				TransitionToState(state);
			}
		}

		Pathfinding.VisualizeCurrentPath();
	}

	public virtual void CaptureTarget(CampBase camp)
	{
		CampToCapture = camp;
		MoveTo(camp.GlobalPosition, State.Capturing);
	}

	public virtual void Stop()
	{
		Steering.Stop();
		TransitionToState(State.Idle);
	}

	public void RotateToAngle(float targetAngle)
	{
		TargetRotation = targetAngle;
		IsRotating = true;
	}

	public void RotateToDirection(Vector2 direction)
	{
		TargetRotation = direction.Angle() + Mathf.Pi/2;
		IsRotating = true;
	}

	public void RotateToTarget(Node2D target)
	{
		if (target == null) return;
		Vector2 direction = (target.GlobalPosition - Unit.GlobalPosition).Normalized();
		RotateToDirection(direction);
	}

	public bool IsRotatedToAngle(float angle)
	{
		float angleDifference = AngleDifference(Unit.Rotation, angle);
		return Mathf.Abs(angleDifference) <= AcceptableAngleError;
	}

	public bool IsRotatedToDirection(Vector2 direction)
	{
		float targetAngle = direction.Angle() + Mathf.Pi/2;
		return IsRotatedToAngle(targetAngle);
	}

	public bool IsRotatedToTarget(Node2D target)
	{
		if (target == null) return false;
		Vector2 direction = (target.GlobalPosition - Unit.GlobalPosition).Normalized();
		return IsRotatedToDirection(direction);
	}

	// ----------------------------------------------------------------------------------
	// State Transition and Processing
	public override void _Process(double delta)
	{
		switch (CurrentState)
		{
			case State.Idle:
				ProcessIdleState(delta);
				break;
			case State.Moving:
				ProcessMovingState(delta);
				break;
			case State.Rotating:
				ProcessRotatingState(delta, OldState);
				break;
			case State.Capturing:
				ProcessCapturingState(delta);
				break;
			case State.Dead:
				// No processing needed for dead state
				break;
		}
	}

	public void TransitionToState(State newState)
	{
		State oldState = CurrentState;
		CurrentState = newState;

		switch (newState)
		{
			case State.Dead:
				Steering.Stop();
				DisconnectAllSignals();
				QueueFree();
				break;
		}

		EmitSignal(SignalName.StateChanged, (int)newState, (int)oldState);
	}

	// The unit will just idle for now, it will be overriden in AttackableUnitFSM to attack enemies
	protected virtual void ProcessIdleState(double delta)
	{
		Steering.Stop();
	}

	protected virtual void ProcessMovingState(double delta)
	{
		if (Steering.CurrentMode == SteeringSystem.SteeringMode.None)
		{
			// If we were moving to capture a camp
			if (CampToCapture != null)
			{
				TransitionToState(State.Capturing);
			}
			else
			{
				TransitionToState(State.Idle);
			}
		}
	}

	protected virtual void ProcessRotatingState(double delta, State? oldState)
	{
		// Handle the actual rotation mechanics
		ProcessRotation(delta);

		// Check if we've finished rotating
		if (!IsRotating)
		{
			if (PathToFollow == null || PathToFollow.Count == 0)
			{
				TransitionToState(State.Idle);
			}
			else if (oldState != null)
			{
				MoveTo(PathToFollow[^1], (State)oldState);
			}
		}
	}

	protected void ProcessRotation(double delta)
	{
		if (IsRotating)
		{
			float currentRotation = Unit.Rotation;
			float angleDifference = AngleDifference(currentRotation, TargetRotation);

			// If we're close enough to target angle
			if (Mathf.Abs(angleDifference) <= AcceptableAngleError)
			{
				IsRotating = false;
				Unit.Rotation = TargetRotation; // Snap to exact angle
			}
			else
			{
				float rotationStep = Mathf.Sign(angleDifference) * RotationSpeed * (float)delta;

				// Make sure we don't over-rotate
				if (Mathf.Abs(rotationStep) > Mathf.Abs(angleDifference))
					rotationStep = angleDifference;

				Unit.Rotation += rotationStep;
			}
		}
	}

	protected virtual void ProcessCapturingState(double delta)
	{
		if (CampToCapture != null)
		{
			if (CampToCapture.CurrentHealth > 0) // If the camp is still alive
			{
				// Just changes to idle state if the camp is not zero health
				TransitionToState(State.Idle);
			}

			float distanceToCamp = Unit.GlobalPosition.DistanceTo(CampToCapture.GlobalPosition);

			if (distanceToCamp <= 75.0f)  // Capture radius
			{
				// Try to capture the camp
				if (CampToCapture.TryCapture((CampBase.CampOwner)Unit.TeamId))
				{
					EmitSignal(SignalName.TargetCaptured, this, CampToCapture);
					CampToCapture = null;
				}
				TransitionToState(State.Idle);
			}
			else
			{
				Steering.Seek(CampToCapture.GlobalPosition);
			}
		}
		else
		{
			// No camp to capture
			TransitionToState(State.Idle);
		}
	}

	protected void OnRetreatTimeout()
	{
		TransitionToState(State.Idle);
		_retreatTimer.QueueFree();
		_retreatTimer = null;
		CurrentTarget = null; // Clear the target after retreating
	}

	protected virtual void OnEnemyDetected(Node2D enemy)
	{
		CurrentTarget = enemy;
		Steering.Stop();
		// For now, just stop when an enemy is detected
	}

	protected virtual void OnCampReducedToZeroHP(CampBase camp)
	{
		// Check if this unit should capture the camp
		if (CurrentState != State.Dead && CurrentState != State.Capturing)
		{

			if (Unit.GlobalPosition.DistanceTo(camp.GlobalPosition) <= 200)
			{
				CaptureTarget(camp);
				TransitionToState(State.Capturing);
			}
			else TransitionToState(State.Idle);
		}
	}

	private float AngleDifference(float from, float to)
	{
		// Calculate the difference between angles
		float diff = to - from;

		// Wrap the angle to be between -PI and PI
		while (diff > Mathf.Pi)
			diff -= Mathf.Tau;
		while (diff < -Mathf.Pi)
			diff += Mathf.Tau;

		return diff;
	}

	public State GetCurrentState() => CurrentState;

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
		Disconnect(nameof(TargetCaptured), new Callable(blackboard, nameof(Blackboard.OnTargetCaptured)));
	}
}
