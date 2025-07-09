using Godot;
using System;
using NeuroWarCommander.Scripts.Camps;

namespace NeuroWarCommander.Scripts.Units.Base.AttackableBase;

public partial class ProjectileBase : Area2D
{
	[Export] public float Speed { get; set; }
	[Export] public float Damage { get; set; }
	[Export] public float MaxRange { get; set; }
	[Export] public int TeamId { get; set; }

	public Vector2 Direction { get; set; }
	public Node2D Source { get; set; }

	private Vector2 _startPosition;
	private bool _initialized = false;

	public override void _Ready()
	{
		_startPosition = GlobalPosition;
		BodyEntered += OnBodyEntered;
	}

	public void Initialize()
	{
		_startPosition = GlobalPosition;
		_initialized = true;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!_initialized) return;

		// Move in the defined direction
		Vector2 movement = Direction * Speed * (float)delta;
		GlobalPosition += movement;

		// Check if we've exceeded max range
		float distanceTraveled = GlobalPosition.DistanceTo(_startPosition);
		if (distanceTraveled >= MaxRange)
		{
			CallDeferred("free");
		}
	}

	protected virtual void OnBodyEntered(Node2D body)
	{
		// Skip collision with the source of the projectile
		if (body == Source) return;

		// GD.Print("Projectile hit: " + body.Name);
		// Check if it's a camp
		if (body is CampBase camp && (int)camp.TeamId != TeamId)
		{
			// GD.Print("Projectile hit camp: " + camp.Name);
			ApplyDamageToCamp(camp);
		}

		// Check if it's an enemy unit
		if (body is UnitBase unit && unit.TeamId != TeamId)
		{
			ApplyDamage(unit, Damage);
		}
	}

	protected void ApplyDamage(UnitBase target, float damage)
	{
		target.TakeDamage(damage, Source);
	}

	private void ApplyDamageToCamp(CampBase camp)
	{
		camp.TakeDamage(Damage);
	}
}
