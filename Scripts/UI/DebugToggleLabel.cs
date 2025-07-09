using Godot;
using System;

namespace NeuroWarCommander.Scripts.UI;

public partial class DebugToggleLabel : Label
{
	private Team.Team _team; // Any team works here
	public override void _Ready()
	{
		CallDeferred(nameof(Initialize));
	}

	public void Initialize()
	{
		_team = GetTree().Root.GetNode<Team.Team>("Game/World/TeamA"); // Any team works here
	}

	public override void _Process(double delta)
	{
		var debugMode = _team.IsDebugging ? "ON" : "OFF";
		Text = $"Debug Mode: {debugMode}";
	}
}
