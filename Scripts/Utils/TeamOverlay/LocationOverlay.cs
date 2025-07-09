using Godot;
using NeuroWarCommander.Scripts.Team.Blackboard;

namespace NeuroWarCommander.Scripts.Utils.TeamOverlay;

public partial class LocationOverlay : Node2D
{
    private LocationMap _locationMap;
    private float _cellSize;
    private Font _font;
    private const float MinimumUpdatedInterval = 3.0f; // minimum time between updates to avoid lagging
    private float _lastUpdateTime = 0.0f;

    public override void _Ready()
    {
        var bb = GetParent().GetParent().GetParent().GetNode<Blackboard>("Blackboard");
        _locationMap = bb.GetNode<LocationMap>("LocationMap");
        _cellSize = _locationMap.CellSize;

        _font = GD.Load<Font>("res://Assets/Fonts/rimouski sb.otf");

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
        foreach (var kvp in _locationMap.LocationCells)
        {
            Vector2I gridPosF = kvp.Key;
            var entity = kvp.Value;
            if (entity == null)
                continue;
            // Convert grid to world
            Vector2 cellWorld = new Vector2(gridPosF.X * _cellSize, gridPosF.Y * _cellSize);

            string label = "";
            Color color = Colors.White;

            switch (entity.Type)
            {
                case LocationMap.EntityType.AllyUnit:
                    label = "A";
                    color = Colors.Blue;
                    break;
                case LocationMap.EntityType.EnemyUnit:
                    label = "E";
                    color = Colors.Red;
                    break;
                case LocationMap.EntityType.AllyCamp:
                    label = "C (A)";
                    color = new Color(0f, 0.5f, 1f); // blue
                    break;
                case LocationMap.EntityType.EnemyCamp:
                    label = "C (E)";
                    color = new Color(1f, 0f, 1f); // magenta
                    break;
                case LocationMap.EntityType.NeutralCamp:
                    label = "C (N)";
                    color = Colors.Gray;
                    break;
                case LocationMap.EntityType.Obstacle:
                    label = "O";
                    color = Colors.Brown;
                    break;
            }

            DrawString(_font, ToLocal(cellWorld + new Vector2(2, _cellSize - 2)), label,
                modulate: color, fontSize: 10);
        }
    }
}