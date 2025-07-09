using System;
using Godot;
using System.Collections.Generic;
using System.Linq;
using NeuroWarCommander.Scripts.Team.Blackboard;

namespace NeuroWarCommander.Scripts.Utils.TeamOverlay;

/// Draws a darkening overlay on every cell that VisionMap has seen.
/// Opacity = clamp((timeSinceSeen) / MaxFadeTime, 0..1) × someFactor.
/// A cell with VisionTime=float.MaxValue is “never seen” and is not drawn here (remains fully black).
public partial class VisionOverlay : Node2D
{
    private VisionMap _visionMap;
    private float _cellSize;
    private const float MaxFadeTime = 20.0f; // after 20 in-game seconds, cell is fully dark
    private const float MinimumUpdatedInterval = 0.75f; // minimum time between updates to avoid lagging
    private float _lastUpdateTime = 0.0f;

    public override void _Ready()
    {
        // Grab VisionMap from Blackboard
        var bb = GetParent().GetParent().GetParent().GetNode<Blackboard>("Blackboard");
        _visionMap = bb.GetNode<VisionMap>("VisionMap");
        _cellSize = _visionMap.CellSize;
    }

    public override void _Process(double delta)
    {
        _lastUpdateTime += (float)delta;
        if (_lastUpdateTime >= MinimumUpdatedInterval)
        {
            _lastUpdateTime = 0.0f; // reset the timer
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        foreach (var kvp in _visionMap.VisionCells)
        {
            Vector2I gridPosF = kvp.Key;
            float seenTime = kvp.Value; // “game time” in seconds since last seen

            Color c = new Color(0f, 0f, 0f, 0.9f);
            // If never seen, skip (float.MaxValue)
            if (!Mathf.IsEqualApprox(seenTime, float.MaxValue))
            {
                float alpha = Mathf.Clamp(seenTime / MaxFadeTime, 0f, 1f);
                c.A = 0.9f * alpha;
            }

            Vector2 pos = new Vector2(gridPosF.X * _cellSize, gridPosF.Y * _cellSize);
            Vector2 globalPos = ToLocal(pos);

            DrawRect(new Rect2(globalPos, new Vector2(_cellSize, _cellSize)), c);
        }
    }
}
