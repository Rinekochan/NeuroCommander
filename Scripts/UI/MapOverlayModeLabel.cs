using Godot;
using System;

namespace NeuroWarCommander.Scripts.UI;

public partial class MapOverlayModeLabel : Label
{
	private World _world;

	public override void _Ready()
	{
		CallDeferred(nameof(Initialize));
	}

	public void Initialize()
	{
		_world = GetTree().Root.GetNode<World>("Game/World");
	}

	public override void _Process(double delta)
	{
		var overlay = _world.CurrentOverlayMode switch
		{
			OverlayMode.Environment => "Environment",
			OverlayMode.Influence => "Influence",
			OverlayMode.Movement => "Movement",
			_ => "Unknown Overlay"
		};

		Text = $"Map Overlay: {overlay}";
	}
}
