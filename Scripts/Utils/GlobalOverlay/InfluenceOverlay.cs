using Godot;
using NeuroWarCommander.Scripts.Utils.BaseMap;

namespace NeuroWarCommander.Scripts.Utils.GlobalOverlay;

public partial class InfluenceOverlay : Node2D
{
    [Export] public NodePath InfluenceMap;
    [Export] public int TeamId { get; set; } = 0;
    private InfluenceMapBase _influenceMap;
    private float _cellSize;
    private const float MinimumUpdatedInterval = 1.0f; // minimum time between updates to avoid lagging
    private float _lastUpdateTime = 0.0f;

    public override void _Ready()
    {
        _influenceMap = GetNode<InfluenceMapBase>(InfluenceMap);
        _cellSize = _influenceMap.CellSize;

        SetProcess(true);
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
        foreach (var kvp in _influenceMap.InfluenceCells)
        {
            Vector2I gridPosF = kvp.Key;
            Vector2 worldPos = new Vector2(gridPosF.X * _cellSize, gridPosF.Y * _cellSize);
            Vector2 localPos = ToLocal(worldPos);
            var cell = kvp.Value;

            Color col = new Color();
            // Only draw if we are “confident” about that cell’s influence
            if (!cell.Confidence)
            {
                // Make the cell black if we are not confident
                col = new Color(0f, 0f, 0f, 0.75f); // semi-transparent black
            }
            else
            {
                float total = cell.TotalInfluence; // positive = ally, negative = enemy
                float mag = Mathf.Clamp(Mathf.Abs(total) / 40f, -1f, 1f);

                if (total == 0) // Just make it dark so we can see it easier
                {
                    col = new Color(0f, 0f, 0f, 0.75f); // semi-transparent black
                }
                else if (total > 0)
                {
                    col = new Color(0f, 0f, 1f, mag); // blue for Team A influence
                }
                else if (total < 0)
                {
                    col = new Color(1f, 0f, 0f, mag); // red for Team B influence
                }
            }

            DrawRect(new Rect2(localPos, new Vector2(_cellSize, _cellSize)), col);
        }
    }
}