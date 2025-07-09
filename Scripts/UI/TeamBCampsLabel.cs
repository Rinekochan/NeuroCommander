using Godot;
using System.Linq;
using System.Collections.Generic;
using NeuroWarCommander.Scripts.Camps;
using NeuroWarCommander.Scripts.Team.Blackboard;

namespace NeuroWarCommander.Scripts.UI;

public partial class TeamBCampsLabel : Label
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
		int numberOfCamps = GetAllCamps().Count(c => (int)c.TeamId == _teamB.TeamId);
		Text = $"Camps: {numberOfCamps}";
	}

	private List<CampBase> GetAllCamps()
	{
		var camps = new List<CampBase>();

		// Find all camps in the map
		var map = GetTree().Root.GetNode("Game/World/Map/Camps");
		if (map != null)
		{
			foreach (var child in map.GetChildren())
			{
				if (child is CampBase camp)
				{
					camps.Add(camp);
				}
			}
		}

		return camps;
	}
}
