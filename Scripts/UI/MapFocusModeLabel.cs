using Godot;
using System;
using NeuroWarCommander.Scripts;

namespace NeuroWarCommander.Scripts.UI;

public partial class MapFocusModeLabel : Label
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
		var focus = _world.CurrentMapFocus switch
		{
			MapFocus.All => "All",
			MapFocus.TeamA => "Team A",
			MapFocus.TeamB => "Team B",
			_ => "Unknown Focus"
		};

		Text = $"Map Focus: {focus}";
	}
}
