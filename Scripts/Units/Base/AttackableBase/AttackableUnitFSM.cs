using Godot;
using System;
using System.Linq;

namespace NeuroWarCommander.Scripts.Units.Base.AttackableBase;

public partial class AttackableUnitFSM : UnitFSM
{
    public new enum State
    {
        Idle = UnitFSM.State.Idle,
        Moving = UnitFSM.State.Moving,
        Capturing = UnitFSM.State.Capturing,
        Rotating = UnitFSM.State.Rotating,
        Dead = UnitFSM.State.Dead,
        Attacking = 7  // New state specific to combat units
    }

    private AttackableUnitBase _attackableUnit;
    private Node2D _attackTarget;

    public override void _Ready()
    {
        base._Ready();
        _attackableUnit = GetParent<AttackableUnitBase>();
    }

    // Command API to attack a target
    public void AttackTarget(Node2D target)
    {
        if (target == null) return;
        _attackTarget = target;

        if (PathToFollow != null && PathToFollow.Count > 0)
        {
            PathToFollow.Clear();
            Steering.Stop();
            TransitionToState((UnitFSM.State)State.Attacking);
        }



        // Check if we need to rotate first
        bool isInFOV = FovCone.CanSeeEntity(target);
        bool isRotated = IsRotatedToTarget(target);

        if (!isRotated)
        {
            // Need to rotate first
            OldState = (UnitFSM.State)State.Attacking;
            RotateToTarget(target);
            TransitionToState(UnitFSM.State.Rotating);
        }
        else if (!isInFOV || !_attackableUnit.CanAttack(target)) // If not in attack range
        {
            MoveTo(target.GlobalPosition, (UnitFSM.State)State.Attacking);
        }
    }

    // ------------------------------------------------------------------------
    // State Transition and Processing
    public override void _Process(double delta)
    {
        if ((int)CurrentState == (int)State.Attacking)
        {
            ProcessAttackingState();
        }
        base._Process(delta);
    }

    // Move to attack state if any enemies are visible when idle/guarding
    protected override void ProcessIdleState(double delta)
    {
        CurrentTarget = FovCone.GetVisibleEnemies().FirstOrDefault(); // Just get the first enemy in FOV
        if (CurrentTarget == null)
        {
            base.ProcessIdleState(delta);
            return;
        }
        AttackTarget(CurrentTarget);
    }

    private void ProcessAttackingState()
    {
        if (_attackTarget == null || !IsInstanceValid(_attackTarget) || _attackTarget.IsQueuedForDeletion())
        {
            _attackTarget = null;
            TransitionToState(UnitFSM.State.Idle);
            return;
        }


        // Check if target is still in perception
        bool isTargetVisible = FovCone.CanSeeEntity(_attackTarget); // No obstacles in the way
        bool isRotatedToTarget = IsRotatedToTarget(_attackTarget);

        if (!isTargetVisible)
        {
            if (PathToFollow == null || PathToFollow.Count == 0)
                MoveTo(_attackTarget.GlobalPosition, (UnitFSM.State)State.Attacking);
            else if (_attackTarget.GlobalPosition.DistanceTo(_attackableUnit.GlobalPosition) < 150 && !isRotatedToTarget)
            {
                PathToFollow = null; // Clear path if close enough to target
                Steering.Stop();
                OldState = (UnitFSM.State)State.Attacking;
                RotateToTarget(_attackTarget);
                TransitionToState(UnitFSM.State.Rotating);
            }

            return;
        }

        // Check if target is in attack range
        if (_attackableUnit.CanAttack(_attackTarget))
        {
            Steering.Stop();

            // We're in range and properly facing, attack!
            _attackableUnit.Attack(_attackTarget);
        }
    }

    protected override void ProcessRotatingState(double delta, UnitFSM.State? oldState)
    {
        // Call the parent method to handle rotation mechanics
        base.ProcessRotatingState(delta, oldState);

        // If rotation is complete and we have an attack target, go to attacking
        if (!IsRotating && _attackTarget != null)
        {
            TransitionToState((UnitFSM.State)State.Attacking);
        }
    }


    public override void MoveTo(Vector2 position, UnitFSM.State state)
    {
        if (state != (UnitFSM.State)State.Attacking)
        {
            _attackTarget = null;
        }

        base.MoveTo(position, state);
    }

    protected override void OnEnemyDetected(Node2D enemy)
    {
        // Only auto-attack if we're idle
        if (CurrentState == (int)State.Idle)
        {
            AttackTarget(enemy);
        }
    }

    public Node2D GetAttackTarget()
    {
        return _attackTarget;
    }
}
