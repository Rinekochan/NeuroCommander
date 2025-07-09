using Godot;
using System;

namespace NeuroWarCommander.Scripts.UI;

public partial class TeamBUnitsLabel : Label
{
	private Team.Team _teamB;
	public override void _Ready()
	{
		CallDeferred(nameof(Initialize));
	}

	public void Initialize()
	{
		_teamB = GetTree().Root.GetNode<Team.Team>("Game/World/TeamB");
	}

	public override void _Process(double delta)
	{
		var numberOfUnits = _teamB.GetNode("Units").GetChildren().Count;
		Text = $"Units: {numberOfUnits}";
	}

}
