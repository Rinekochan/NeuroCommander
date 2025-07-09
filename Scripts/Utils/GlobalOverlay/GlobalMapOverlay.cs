using Godot;
using System;

namespace NeuroWarCommander.Scripts.Utils.GlobalOverlay;

public partial class GlobalMapOverlay : Node2D
{
    public OverlayMode OverlayMode { get; set; }
    private Node2D _moveOverlay;
    private Node2D _infOverlay;

    public override void _Ready()
    {
        _infOverlay = GetNode<Node2D>("GlobalInfluenceOverlay");

        _infOverlay.Visible = false;
    }

    public void Reset()
    {
        OverlayMode = OverlayMode.Environment;
        _infOverlay.Visible = false;
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey ev && ev.Pressed)
        {
            // E = Environment mode
            if (ev.Keycode == Key.E)
            {
                _infOverlay.Visible = false;
                OverlayMode = OverlayMode.Environment;
            }
            // M = Movement mode
            else if (ev.Keycode == Key.M)
            {
                _infOverlay.Visible = false;
                OverlayMode = OverlayMode.Movement;
            }
            // I = Influence mode
            else if (ev.Keycode == Key.I)
            {
                _infOverlay.Visible = true;
                OverlayMode = OverlayMode.Influence;
            }
            // Note: “G” is handled inside MovementOverlay.cs
        }
    }
}