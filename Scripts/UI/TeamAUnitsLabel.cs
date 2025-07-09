using Godot;
using System;

namespace NeuroWarCommander.Scripts.UI;

public partial class TeamAUnitsLabel : Label
{
	private Team.Team _teamA;
	public override void _Ready()
	{
		CallDeferred(nameof(Initialize));
	}

	public void Initialize()
	{
		_teamA = GetTree().Root.GetNode<Team.Team>("Game/World/TeamA");
	}

	public override void _Process(double delta)
	{
		var numberOfUnits = _teamA.GetNode("Units").GetChildren().Count;
		Text = $"Units: {numberOfUnits}";
	}
}
