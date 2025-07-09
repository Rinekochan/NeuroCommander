using Godot;
using System;
using Godot.Collections;
using NeuroWarCommander.Scripts.Units.Base.AttackableBase;
using NeuroWarCommander.Scripts.Utils.GlobalOverlay;
using NeuroWarCommander.Scripts.Utils.TeamOverlay;

namespace NeuroWarCommander.Scripts;

public enum MapFocus
{
    All, // Global map overlay showing environment
    TeamA, // Team A's tactical map overlay
    TeamB // Team B's tactical map overlay
}

public enum OverlayMode
{
    Environment, // Show environment (default)
    Influence, // Show influence map
    Movement // Show movements on the tactical map
}

public partial class World : Node2D
{
    public MapFocus CurrentMapFocus { get; private set; } = MapFocus.All;
    public OverlayMode CurrentOverlayMode { get; private set; } = OverlayMode.Environment;

    private Node2D _projectilesContainer;

    // References to each team’s TacticalMapOverlay
    private TacticalMapOverlay _teamAOverlay;
    private TacticalMapOverlay _teamBOverlay;
    private GlobalMapOverlay _globalOverlay;

    private Array<Node> _teamAUnits;
    private Array<Node> _teamBUnits;


    public override void _Ready()
    {
        // Create a container for projectiles
        _projectilesContainer = new Node2D { Name = "Projectiles" };
        AddChild(_projectilesContainer);

        // Cache references to TeamA and TeamB overlays
        _teamAOverlay = GetNode<TacticalMapOverlay>("TeamA/TacticalMapOverlay");
        _teamBOverlay = GetNode<TacticalMapOverlay>("TeamB/TacticalMapOverlay");
        _globalOverlay = GetNode<GlobalMapOverlay>("GlobalMapOverlay");

        // At startup, hide both team overlays (world‐default view)
        if (_teamAOverlay != null)
            _teamAOverlay.Visible = false;
        if (_teamBOverlay != null)
            _teamBOverlay.Visible = false;

        _globalOverlay.Visible = true;

        CallDeferred(nameof(UpdateChildren));
    }

    public void UpdateChildren()
    {
        // References to Team A and Team B units also
        _teamAUnits = GetNode<Node2D>("TeamA").GetNode("Units").GetChildren();
        _teamBUnits = GetNode<Node2D>("TeamB").GetNode("Units").GetChildren();
    }

    public override void _Process(double delta)
    {
        UpdateChildren();
        if (CurrentMapFocus == MapFocus.All)
        {
            CurrentOverlayMode = _globalOverlay.OverlayMode;
        }
        else if (CurrentMapFocus == MapFocus.TeamA && _teamAOverlay != null)
        {
            CurrentOverlayMode = _teamAOverlay.OverlayMode;
        }
        else if (CurrentMapFocus == MapFocus.TeamB && _teamBOverlay != null)
        {
            CurrentOverlayMode = _teamBOverlay.OverlayMode;
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey ev && ev.Pressed)
        {
            // F1: default world view (no overlays)
            if (ev.Keycode == Key.F1)
            {
                if (_teamAOverlay  != null) _teamAOverlay.Visible = false;
                if (_teamBOverlay  != null) _teamBOverlay.Visible = false;
                if (_globalOverlay != null) _globalOverlay.Visible = true;

                _globalOverlay.OverlayMode = OverlayMode.Environment;

                CurrentMapFocus = MapFocus.All;
                CurrentOverlayMode = OverlayMode.Environment;

                _teamAOverlay?.Reset();
                _teamBOverlay?.Reset();
                _globalOverlay?.Reset();

                ShowTeamAUnits();
                ShowTeamBUnits();
            }
            // F2: show Team A’s overlay in Environment mode by default, and then hide team B
            else if (ev.Keycode == Key.F2)
            {
                if (_teamAOverlay  != null) _teamAOverlay.Visible = true;
                if (_teamBOverlay  != null) _teamBOverlay.Visible = false;
                if (_globalOverlay != null) _globalOverlay.Visible = false;

                // Ensure Team A’s overlay starts in Environment mode:
                _teamAOverlay?.ShowEnvironment();
                CurrentMapFocus = MapFocus.TeamA;
                CurrentOverlayMode = OverlayMode.Environment;

                _teamAOverlay?.Reset();
                _teamBOverlay?.Reset();
                _globalOverlay?.Reset();

                ShowTeamAUnits();
                HideTeamBUnits();
            }
            // F3: show Team B’s overlay in Environment mode by default
            else if (ev.Keycode == Key.F3)
            {
                if (_teamAOverlay  != null) _teamAOverlay.Visible = false;
                if (_teamBOverlay  != null) _teamBOverlay.Visible = true;
                if (_globalOverlay != null) _globalOverlay.Visible = false;

                // Ensure Team B’s overlay starts in Environment mode:
                _teamBOverlay?.ShowEnvironment();
                CurrentMapFocus = MapFocus.TeamB;
                CurrentOverlayMode = OverlayMode.Environment;

                _teamAOverlay?.Reset();
                _teamBOverlay?.Reset();
                _globalOverlay?.Reset();

                HideTeamAUnits();
                ShowTeamBUnits();
            }
        }
    }

    // Method for units to register their projectiles with the world
    public void AddProjectile(ProjectileBase projectile)
    {
        // Ensure projectile is parented to projectiles container for organization
        if (projectile.GetParent() != _projectilesContainer)
        {
            if (projectile.GetParent() != null)
            {
                projectile.GetParent().RemoveChild(projectile);
            }
            _projectilesContainer.AddChild(projectile);
        }
    }

    // Show Team A Units
    public void ShowTeamAUnits()
    {
        UpdateChildren();
        foreach (var unit in _teamAUnits)
        {
            ((Node2D)unit).Visible = true;
        }
    }

    // Hide Team A Units
    public void HideTeamAUnits()
    {
        UpdateChildren();
        foreach (var unit in _teamAUnits)
        {
            ((Node2D)unit).Visible = false;
        }
    }

    // Show Team B Units
    public void ShowTeamBUnits()
    {
        UpdateChildren();
        foreach (var unit in _teamBUnits)
        {
            ((Node2D)unit).Visible = true;
        }
    }

    // Hide Team B Units
    public void HideTeamBUnits()
    {
        UpdateChildren();
        foreach (var unit in _teamBUnits)
        {
            ((Node2D)unit).Visible = false;
        }
    }
}
