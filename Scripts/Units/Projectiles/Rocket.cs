using Godot;
using System;
using NeuroWarCommander.Scripts.Units.Base;
using NeuroWarCommander.Scripts.Units.Base.AttackableBase;

namespace NeuroWarCommander.Scripts.Units.Projectiles;

public partial class Rocket : ProjectileBase
{
    [Export] public float ExplosionRadius { get; set; } = 100.0f;
    [Export] public float ExplosionDamage { get; set; } = 15.0f;
    [Export] public PackedScene ExplosionEffectScene { get; set; }
    [Export] public float FuseTime { get; set; } = 0.1f; // Small delay before detonating on impact

    private Timer _fuseTimer;

    public override void _Ready()
    {
        base._Ready();

        _fuseTimer = new Timer();
        _fuseTimer.OneShot = true;
        _fuseTimer.WaitTime = FuseTime;
        _fuseTimer.Timeout += OnFuseTimeout;
        AddChild(_fuseTimer);
    }

    protected override void OnBodyEntered(Node2D body)
    {
        if (body == Source) return;

        base.OnBodyEntered(body);
        _fuseTimer.Start();

        // Stop moving
        Set("_initialized", false);
    }

    private void OnFuseTimeout()
    {
        Explode();
    }

    private void Explode()
    {
        // Create explosion effect
        if (ExplosionEffectScene != null)
        {
            var explosion = ExplosionEffectScene.Instantiate<Node2D>();
            GetTree().Root.AddChild(explosion);
            explosion.GlobalPosition = GlobalPosition;

            var animPlayer = explosion.GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
            if (animPlayer != null)
            {
                // Connect to animation finished signal to free the node when done
                animPlayer.Connect(AnimationPlayer.SignalName.AnimationFinished,
                    Callable.From((string animName) => explosion.QueueFree()));

                // Play the animation
                if (animPlayer.HasAnimation("Hit"))
                    animPlayer.Play("Hit");
                else if (animPlayer.HasAnimation("explode"))
                    animPlayer.Play("explode");
                else
                    animPlayer.Play();
            }
            else
            {
                // No animation player, set a timer to remove it after a delay
                var timer = new Timer();
                timer.WaitTime = 1.0f;
                timer.OneShot = true;
                explosion.AddChild(timer);
                timer.Timeout += () => explosion.QueueFree();
                timer.Start();
            }
        }

        // Find all units in blast radius
        var spaceState = GetWorld2D().DirectSpaceState;
        var queryParams = new PhysicsShapeQueryParameters2D();

        var shape = new CircleShape2D();
        shape.Radius = ExplosionRadius;

        queryParams.Shape = shape;
        queryParams.Transform = new Transform2D(0, GlobalPosition);
        queryParams.CollideWithAreas = true;
        queryParams.CollideWithBodies = true;

        var results = spaceState.IntersectShape(queryParams);

        // Apply damage to all units in blast radius
        foreach (var result in results)
        {
            var collider = result["collider"].As<Node2D>();

            // Skip the source unit
            if (collider == Source) continue;

            if (collider is UnitBase unit && unit.TeamId != TeamId)
            {
                // Calculate damage based on distance from explosion center
                float distance = GlobalPosition.DistanceTo(unit.GlobalPosition);
                float damageMultiplier = 1.0f - Mathf.Clamp(distance / ExplosionRadius, 0, 1);
                float damage = ExplosionDamage * damageMultiplier;

                ApplyDamage(unit, damage);
            }
        }

        QueueFree();
    }
}
