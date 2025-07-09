using System.Collections.Generic;
using Godot;
using NeuroWarCommander.Scripts.Units.Base;

namespace NeuroWarCommander.Scripts.Utils.BaseMap;

public partial class InfluenceMapBase : Node
{
    public class InfluenceCell
    {
        public float AllyInfluence;
        public float EnemyInfluence;
        public float TotalInfluence => AllyInfluence - EnemyInfluence;
        public bool Confidence;

        public InfluenceCell(float allyInfluence, float enemyInfluence)
        {
            AllyInfluence = allyInfluence;
            EnemyInfluence = enemyInfluence;
        }
    }

    [Export] public Vector2I MapSize = new(2048, 2048);
    [Export] public Vector2I MapCenter = Vector2I.Zero;
    [Export] public int CellSize = 64; // We want to evaluate influence by regions to avoid performance issues

    public Dictionary<Vector2I, InfluenceCell> InfluenceCells { get; private set; } = new();


    protected Rect2 _mapBounds;
    protected Grid _grid; // Support for position-based grid operations

    public override void _Ready()
    {
        _grid = new Grid();

        int halfWidth = MapSize.X / 2;
        int halfHeight = MapSize.Y / 2;
        _mapBounds = new Rect2I(
            MapCenter.X - halfWidth,
            MapCenter.Y - halfHeight,
            MapSize.X,
            MapSize.Y
        );

        CallDeferred(nameof(Initialize));
    }

    public virtual void Initialize()
    {
        UpdateGrid();
    }

    public virtual void UpdateGrid() {}
    public virtual void UpdateConfidence() {}

    // We want a region to spread its influence divided by square of distance, and we also only limit the radius of 4 cells
    protected void SpreadInfluence(Vector2I influencePosition, float influenceValue, bool isAlly = true)
    {
        for (int xOffset = -3; xOffset <= 3; xOffset++)
        {
            for (int yOffset = -3; yOffset <= 3; yOffset++)
            {
                float distance = Mathf.Sqrt(xOffset * xOffset + yOffset * yOffset);
                if (distance == 0.0f || distance > 3.0f) continue; // Limit the radius to 4 cells

                distance += 1.0f;

                Vector2I neighborPosition = influencePosition + new Vector2I(xOffset, yOffset);
                if (InfluenceCells.TryGetValue(neighborPosition, out var cell))
                {
                    if (isAlly)
                    {
                        cell.AllyInfluence += influenceValue / distance;
                    }
                    else
                    {
                        cell.EnemyInfluence += influenceValue / distance;
                    }
                }
            }
        }
    }

    protected void VisualiseGrid()
    {
        int columns = Mathf.CeilToInt(_mapBounds.Size.X / CellSize);
        int rows = Mathf.CeilToInt(_mapBounds.Size.Y / CellSize);

        GD.Print($"Influence Map: {columns} columns, {rows} rows, Cell Size: {CellSize}");

        var startPos = -(MapSize / 2 / CellSize);
        var endPos = MapSize / 2 / CellSize;

        for (int x = startPos.X; x < endPos.X; x++)
        {
            string rowText = "";
            for (int y = startPos.Y; y < endPos.Y; y++)
            {
                Vector2I pos = new Vector2I(x, y);

                rowText += $"{InfluenceCells[pos].TotalInfluence} | ";
            }

            GD.Print(rowText);
            GD.Print("--------------------------------------------");
        }
    }

    protected static float GetUnitInfluence(UnitBase unit)
    {
        if (unit == null || unit.CurrentHealth <= 0)
        {
            return 0.0f; // No influence if the unit is null or destroyed
        }

        var influence = 2.0f; // Default influence for scouts, medics, and commanders because they can't attack
        if (unit.IsInGroup("rifles"))
        {
            influence = 7.5f;
        }
        else if (unit.IsInGroup("snipers"))
        {
            influence = 10.0f;
        }
        else if (unit.IsInGroup("tankers"))
        {
            influence = 6.0f;
        }
        else if (unit.IsInGroup("siege_machines"))
        {
            influence = 10.0f;
        }

        return influence * (unit.CurrentHealth / unit.MaxHealth);
    }
}
