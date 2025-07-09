using Godot;

namespace NeuroWarCommander.Scripts.Utils.TeamOverlay;

public partial class TacticalMapOverlay : Node2D
{
	public OverlayMode OverlayMode = OverlayMode.Environment;

	private Node2D _envOverlay;
	private Node2D _infOverlay;

	public override void _Ready()
	{
		// Cache references to each overlay child
		_envOverlay  = GetNode<Node2D>("EnvironmentOverlay");
		_infOverlay  = GetNode<Node2D>("InfluenceOverlay");

		// At startup, show the Environment mode by default (team-focused)
		ShowEnvironment();
	}

	public void Reset()
	{
		ShowEnvironment();
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventKey ev && ev.Pressed)
		{
			// E = Environment mode
			if (ev.Keycode == Key.E)
			{
				ShowEnvironment();
			}
			// M = Movement mode
			else if (ev.Keycode == Key.M)
			{
				ShowMovement();
			}
			// I = Influence mode
			else if (ev.Keycode == Key.I)
			{
				ShowInfluence();
			}
			// Note: “G” is handled inside MovementOverlay.cs
		}
	}

	public void ShowEnvironment()
	{
		OverlayMode = OverlayMode.Environment;
		_envOverlay.Visible  = true;
		_infOverlay.Visible  = false;
	}

	public void ShowMovement()
	{
		OverlayMode = OverlayMode.Movement;
		_envOverlay.Visible  = false;
		_infOverlay.Visible  = false;
	}

	public void ShowInfluence()
	{
		OverlayMode = OverlayMode.Influence;
		_envOverlay.Visible  = false;
		_infOverlay.Visible  = true;
	}
}

