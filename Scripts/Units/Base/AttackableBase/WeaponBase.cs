using Godot;
using System;

namespace NeuroWarCommander.Scripts.Units.Base.AttackableBase;

public partial class WeaponBase : Node2D
{
    [Export] public PackedScene ProjectilePrefab { get; set; }
    [Export] public int MaxAmmo { get; set; }
    [Export] public int CurrentAmmo { get; set; }
    [Export] public float ReloadTime { get; set; }
    [Export] public float FireRate { get; set; } // Time between shots in seconds
    [Export] public float WeaponRange { get; set; }
    [Export] public float ProjectileSpeed { get; set; }
    [Export] public float ProjectileSize { get; set; }
    [Export] public float Damage { get; set; }
    [Export] public float Accuracy { get; set; }// 1.0 = perfect accuracy
    [Export] public int ProjectilesPerShot { get; set; }// For shotguns etc.
    [Export] public float ProjectileSpread { get; set; } = 0.1f; // For weapons with multiple projectiles

    [Signal] public delegate void WeaponFiredEventHandler(Node2D target, int remainingAmmo);
    [Signal] public delegate void WeaponEmptyEventHandler();
    [Signal] public delegate void ReloadStartedEventHandler();
    [Signal] public delegate void ReloadCompletedEventHandler();

    public bool IsReloading;

    private double _timeSinceLastShot;
    private double _reloadTimeRemaining;
    private bool _canFire = true;
    private Marker2D _marker;
    private UnitBase _weaponOwner;

    public override void _Ready()
    {
        _weaponOwner = GetParent<UnitBase>();
        if (_weaponOwner == null)
        {
            GD.PrintErr("WeaponBase must be a child of UnitBase");
        }

        _marker = _weaponOwner.GetNode<Marker2D>("Marker");
        if (_marker == null)
        {
            GD.PrintErr("WeaponBase requires a Marker2D child named 'Marker'");
        }
    }

    public override void _Process(double delta)
    {
        // Handle cooldown between shots
        if (!_canFire)
        {
            _timeSinceLastShot += delta;
            if (_timeSinceLastShot >= FireRate)
            {
                _canFire = true;
            }
        }

        // Handle reloading
        if (IsReloading)
        {
            _reloadTimeRemaining -= delta;
            if (_reloadTimeRemaining <= 0)
            {
                CompleteReload();
            }
        }
    }

    public bool CanFire()
    {
        return _canFire && !IsReloading && CurrentAmmo > 0;
    }

    public bool Fire(Node2D target)
    {
        if (!CanFire()) return false;

        // Calculate target position
        Vector2 targetPos = PredictTargetPosition(target);

        // Mark weapon as fired and start cooldown
        _canFire = false;
        _timeSinceLastShot = 0;

        // For multiple projectiles (like shotgun)
        for (int i = 0; i < ProjectilesPerShot; i++)
        {
            Vector2 direction = (targetPos - _marker.GlobalPosition).Normalized();

            if (Accuracy < 1.0f || ProjectilesPerShot > 1)
            {
                float maxSpreadAngle = 120 * (Mathf.Pi / 180);

                // Scale spread based on accuracy and projectile count
                float spreadFactor = (1.0f - Accuracy) + (ProjectileSpread * (i / (float)ProjectilesPerShot));

                float angle = (float)GD.RandRange(-maxSpreadAngle, maxSpreadAngle) * spreadFactor;
                direction = direction.Rotated(angle);
            }

            SpawnProjectile(direction);
        }

        CurrentAmmo--;
        if (CurrentAmmo <= 0)
        {
            EmitSignal(SignalName.WeaponEmpty);
            return false;
        }

        EmitSignal(SignalName.WeaponFired, target, CurrentAmmo);
        return true;
    }

    public bool StartReload()
    {
        if (IsReloading || CurrentAmmo == MaxAmmo) return false;

        IsReloading = true;
        _reloadTimeRemaining = ReloadTime;
        EmitSignal(SignalName.ReloadStarted);
        return true;
    }

    protected virtual void SpawnProjectile(Vector2 direction)
    {
        if (ProjectilePrefab == null) return;

        var projectile = ProjectilePrefab.Instantiate<ProjectileBase>();

        var world = GetTree().Root.GetNodeOrNull<World>("World");
        if (world != null)
        {
            world.AddProjectile(projectile);
        }
        else
        {
            // Fallback if World node not found (Mainly for testing)
            GetTree().Root.AddChild(projectile);
        }

        // Initialize and shoot the projectile
        projectile.GlobalPosition = _marker.GlobalPosition;
        projectile.Direction = direction;

        projectile.Rotation = direction.Angle() + Mathf.Pi/2;

        projectile.Speed = ProjectileSpeed;
        projectile.MaxRange = WeaponRange;
        projectile.Damage = Damage;
        projectile.TeamId = _weaponOwner.TeamId;
        projectile.Source = _weaponOwner;
        projectile.Scale *= ProjectileSize;

        projectile.Initialize();
    }

    private Vector2 PredictTargetPosition(Node2D target)
    {
        if (target == null || ProjectileSpeed <= 0)
            return target.GlobalPosition;

        // Get target velocity - if it's a physics body
        Vector2 targetVelocity = Vector2.Zero;
        if (target is CharacterBody2D characterBody)
        {
            targetVelocity = characterBody.Velocity;
        }
        else if (target is RigidBody2D rigidBody)
        {
            targetVelocity = rigidBody.LinearVelocity;
        }

        // If target isn't moving significantly, just use current position
        if (targetVelocity.LengthSquared() < 10)
            return target.GlobalPosition;

        Vector2 shooterPos = _marker.GlobalPosition;
        Vector2 targetPos = target.GlobalPosition;

        // Vector from shooter to target (D in the formula)
        Vector2 displacement = targetPos - shooterPos;

        // Solve the quadratic equation: (||V||² - S²)t² + 2(D·V)t + ||D||² = 0
        float a = targetVelocity.LengthSquared() - ProjectileSpeed * ProjectileSpeed;
        float b = 2 * displacement.Dot(targetVelocity);
        float c = displacement.LengthSquared();

        Vector2 predictedPos;

        // Handle different cases based on the value of 'a'
        if (Mathf.Abs(a) < 0.001f)
        {
            if (Mathf.Abs(b) < 0.001f)
                return targetPos; // No solution, use current position

            float t = -c / b;
            if (t < 0)
                return targetPos; // Target moving away, use current position

            predictedPos = targetPos + targetVelocity * t;
        }
        else
        {
            float discriminant = b * b - 4 * a * c;

            if (discriminant < 0)
                return targetPos; // No real solution, use current position

            // Calculate both potential intercept times
            float t1 = (-b + Mathf.Sqrt(discriminant)) / (2 * a);
            float t2 = (-b - Mathf.Sqrt(discriminant)) / (2 * a);

            // Use the smallest positive time
            float t;
            if (t1 < 0 && t2 < 0)
                return targetPos; // Both solutions are negative, use current position

            if (t1 < 0)
                t = t2;
            else if (t2 < 0)
                t = t1;
            else
                t = Mathf.Min(t1, t2); // Both positive, use earliest intercept

            predictedPos = targetPos + targetVelocity * t;
        }

        // Check if predicted position is within weapon range
        float distance = shooterPos.DistanceTo(predictedPos);
        if (distance > WeaponRange)
        {
            // Scale back to maximum range
            Vector2 direction = (predictedPos - shooterPos).Normalized();
            predictedPos = shooterPos + direction * WeaponRange;
        }

        return predictedPos;
    }

    private void CompleteReload()
    {
        IsReloading = false;
        CurrentAmmo = MaxAmmo;
        EmitSignal(SignalName.ReloadCompleted);
    }
}