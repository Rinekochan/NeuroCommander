using Godot;
using System;
using NeuroWarCommander.Scripts.Team.Blackboard;

namespace NeuroWarCommander.Scripts.Units.Base.AttackableBase;

public partial class AttackableUnitBase : UnitBase
{
    [Export] public NodePath WeaponPath { get; set; }
    [Export] public float MinAttackRange { get; set; } = 0.0f; // Minimum distance to attack

    [Signal] public delegate void WeaponFiredEventHandler(Node2D source, Node2D target, int remainingAmmo);
    [Signal] public delegate void WeaponEmptyEventHandler(Node2D source);
    [Signal] public delegate void ReloadStartedEventHandler(Node2D source);
    [Signal] public delegate void ReloadCompletedEventHandler(Node2D source);

    public bool ShowAmmoBar = true;

    private WeaponBase _weapon;

    public override void _Ready()
    {
        base._Ready();

        // Get weapon reference
        if (!string.IsNullOrEmpty(WeaponPath))
        {
            _weapon = GetNode<WeaponBase>(WeaponPath);

            // Connect weapon signals
            if (_weapon != null)
            {
                _weapon.WeaponFired += OnWeaponFired;
                _weapon.WeaponEmpty += OnWeaponEmpty;
                _weapon.ReloadStarted += OnReloadStarted;
                _weapon.ReloadCompleted += OnReloadCompleted;
            }
        }
    }

    public bool CanAttack(Node2D target)
    {
        if (target == null || _weapon == null) return false;

        float distance = GlobalPosition.DistanceTo(target.GlobalPosition);
        return distance >= MinAttackRange &&
               _weapon.CanFire();
    }

    public bool Attack(Node2D target)
    {
        if (!CanAttack(target)) return false;

        // We assume the FSM handles rotation
        bool fired = _weapon.Fire(target);

        return fired;
    }

    public bool StartReload()
    {
        if (_weapon == null) return false;
        return _weapon.StartReload();
    }

    public bool IsWeaponReloading()
    {
        return _weapon != null && _weapon.IsReloading;
    }

    protected override void Die()
    {
        // Get the blackboard node
        var blackboard = GetParent().GetParent().GetNode<Blackboard>("Blackboard");

        // Then disconnect all signals from the unit itself
        Disconnect(nameof(WeaponFired), new Callable(blackboard, nameof(Blackboard.OnWeaponFired)));
        Disconnect(nameof(WeaponEmpty), new Callable(blackboard, nameof(Blackboard.OnWeaponEmpty)));
        Disconnect(nameof(ReloadStarted), new Callable(blackboard, nameof(Blackboard.OnReloadStarted)));
        Disconnect(nameof(ReloadCompleted), new Callable(blackboard, nameof(Blackboard.OnReloadCompleted)));

        base.Die();
    }

    private void OnWeaponFired(Node2D target, int remainingAmmo)
    {
        // Emit the signal to notify that the weapon has fired
        EmitSignal(SignalName.WeaponFired, this, target, remainingAmmo);
    }

    private void OnWeaponEmpty()
    {
        EmitSignal(SignalName.WeaponEmpty, this);
        StartReload();
    }

    private void OnReloadStarted()
    {
        EmitSignal(SignalName.ReloadStarted, this);
    }

    private void OnReloadCompleted()
    {
        EmitSignal(SignalName.ReloadCompleted, this);
    }

    public override void _Draw()
    {
        base._Draw();

        if (!IsDebugging || !ShowAmmoBar || _weapon == null) return;

        // Draw ammo bar above the unit
        Vector2 ammoBarPos = ToLocal(GlobalPosition) + new Vector2(-20, 70);
        float ammoPercent = (float)_weapon.CurrentAmmo / _weapon.MaxAmmo;

        DrawRect(new Rect2(ammoBarPos, new Vector2(40 * ammoPercent, 10)), Colors.Red);
    }
}
