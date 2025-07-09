using Godot;
using System;
using NeuroWarCommander.Scripts.Units.Base.AttackableBase;

namespace NeuroWarCommander.Scripts.Units.Projectiles;

public partial class RifleBullet : ProjectileBase
{
	[Export] public PackedScene HitEffectScene { get; set; }

	protected override void OnBodyEntered(Node2D body)
	{
		// Skip collision with the source
		if (body == Source) return;

		if (HitEffectScene != null)
		{
			var hitEffect = HitEffectScene.Instantiate<Node2D>();
			GetTree().Root.AddChild(hitEffect);
			hitEffect.GlobalPosition = GlobalPosition;

			var animPlayer = hitEffect.GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
			if (animPlayer != null)
			{
				// Connect to animation finished signal to free the node when done
				animPlayer.Connect(AnimationPlayer.SignalName.AnimationFinished,
					Callable.From((string animName) => hitEffect.QueueFree()));

				// Play the animation
				if (animPlayer.HasAnimation("Hit"))
					animPlayer.Play("Hit");
				else if (animPlayer.HasAnimation("default"))
					animPlayer.Play("default");
				else
					animPlayer.Play();
			}
			else
			{
				// No animation player, set a timer to remove it after a delay
				var timer = new Timer();
				timer.WaitTime = 0.5f;
				timer.OneShot = true;
				hitEffect.AddChild(timer);
				timer.Timeout += () => hitEffect.QueueFree();
				timer.Start();
			}
		}
		base.OnBodyEntered(body);

		QueueFree();
	}
}
