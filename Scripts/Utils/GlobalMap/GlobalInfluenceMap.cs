using Godot;
using Godot.Collections;
using NeuroWarCommander.Scripts.Team.Blackboard;
using NeuroWarCommander.Scripts.Units.Base;
using NeuroWarCommander.Scripts.Utils.BaseMap;

namespace NeuroWarCommander.Scripts.Utils.GlobalMap;

public partial class GlobalInfluenceMap : InfluenceMapBase
{
    private Array<Node> _teamAUnits;
    private Array<Node> _teamBUnits;
    private float _updateInterval = 1.0f;

    public override void Initialize()
    {
        _teamAUnits = GetParent().GetParent().GetNode<Node>("TeamA/Units").GetChildren();
        _teamBUnits = GetParent().GetParent().GetNode<Node>("TeamB/Units").GetChildren();

        base.Initialize();
    }

    public override void _Process(double delta)
    {
        _updateInterval -= (float)delta;
        if (_updateInterval <= 0.0f)
        {
            _updateInterval = 1.0f;
            _teamAUnits = GetParent().GetParent().GetNode<Node>("TeamA/Units").GetChildren();
            _teamBUnits = GetParent().GetParent().GetNode<Node>("TeamB/Units").GetChildren();
            UpdateGrid();
        }
    }

    public override void UpdateGrid()
    {
        UpdateConfidence();
        foreach (var unitNode in _teamAUnits)
        {
            if (unitNode is UnitBase unit)
            {
                if(unit == null || !unit.IsInsideTree()) continue; // Skip if unit is not valid or not in the tree
                Vector2I influencePosition = new Vector2I((int)(unit.GlobalPosition.X / CellSize), (int)(unit.GlobalPosition.Y / CellSize));
                float influence = GetUnitInfluence(unit);

                if (InfluenceCells == null || !InfluenceCells.ContainsKey(influencePosition)) continue;
                InfluenceCells[influencePosition].AllyInfluence += influence;
                SpreadInfluence(influencePosition, influence, isAlly: true); // Spread influence to neighboring cells
            }
        }

        foreach (var unitNode in _teamBUnits)
        {
            if (unitNode is UnitBase unit)
            {
                if(unit == null || !unit.IsInsideTree()) continue; // Skip if unit is not valid or not in the tree
                Vector2I influencePosition = new Vector2I((int)(unit.GlobalPosition.X / CellSize), (int)(unit.GlobalPosition.Y / CellSize));
                float influence = GetUnitInfluence(unit);

                if (InfluenceCells == null || !InfluenceCells.ContainsKey(influencePosition)) continue;
                InfluenceCells[influencePosition].EnemyInfluence += influence;
                SpreadInfluence(influencePosition, influence, isAlly: false); // Spread influence to neighboring cells
            }
        }

        // VisualiseGrid();
    }

    public override void UpdateConfidence()
    {
        var startPos = -(MapSize / 2 / CellSize);
        var endPos = MapSize / 2 / CellSize;

        for (int x = startPos.X; x < endPos.X; x++)
        {
            for (int y = startPos.Y; y < endPos.Y; y++)
            {
                Vector2I pos = new Vector2I(x, y);
                {
                    InfluenceCells[pos] = new InfluenceCell(0.0f, 0.0f);
                }

                InfluenceCells[pos].Confidence = true;
            }
        }
    }
}