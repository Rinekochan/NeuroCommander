using Godot;
using NeuroWarCommander.Scripts.Units.Base;
using NeuroWarCommander.Scripts.Utils.BaseMap;

namespace NeuroWarCommander.Scripts.Team.Blackboard;

public partial class InfluenceMap : InfluenceMapBase
{
    public override void UpdateGrid()
    {
        var startPos = -(MapSize / 2 / CellSize);
        var endPos = MapSize / 2 / CellSize;

        for (int x = startPos.X; x < endPos.X; x++)
        {
            for (int y = startPos.Y; y < endPos.Y; y++)
            {
                Vector2I pos = new Vector2I(x, y);

                // Initialize influence cells with zero influence
                if (!InfluenceCells.ContainsKey(pos))
                {
                    InfluenceCells[pos] = new InfluenceCell(0.0f, 0.0f);
                }
                else
                {
                    // Reset influence values if the cell already exists
                    InfluenceCells[pos].AllyInfluence = 0.0f;
                    InfluenceCells[pos].EnemyInfluence = 0.0f;
                }
            }
        }

        var _locationMap = GetParent().GetNode<LocationMap>("LocationMap");

        foreach (var kvp in _locationMap.GetAllUnits())
        {
            Vector2I unitPosition = kvp.Item1;
            UnitBase unit = kvp.Item2.EntityNode as UnitBase;
            if (unit == null) continue; // SKip because maybe the LocationMap is not updated yet

            LocationMap.EntityType unitType = kvp.Item2.Type;

            // We convert the 16x16 unit position to a 64x64 influence cell position
            Vector2I influencePosition = new Vector2I(unitPosition.X / 4, unitPosition.Y / 4);
            if (!InfluenceCells.ContainsKey(influencePosition)) continue;

            var influence = GetUnitInfluence(unit);
            if (unitType == LocationMap.EntityType.AllyUnit)
            {
                InfluenceCells[influencePosition].AllyInfluence += influence;
                SpreadInfluence(influencePosition, influence, isAlly: true); // Spread influence to neighboring cells
            }
            else
            {
                InfluenceCells[influencePosition].EnemyInfluence += influence;
                SpreadInfluence(influencePosition, influence, isAlly: false); // Spread influence to neighboring cells
            }// Spread influence to neighboring cells
        }

        // VisualiseGrid();
    }

    public override void UpdateConfidence()
    {
        var _visionMap = GetParent().GetNode<VisionMap>("VisionMap");

        var startPos = -(MapSize / 2 / CellSize);
        var endPos = MapSize / 2 / CellSize;

        for (int x = startPos.X; x < endPos.X; x++)
        {
            for (int y = startPos.Y; y < endPos.Y; y++)
            {
                Vector2I pos = new Vector2I(x, y);
                // We need to check 16 cells around the position to determine influence since we are using a larger cell size
                int numberOfSeenCells = 0;
                for (int xOffset = 0; xOffset < 4; xOffset++)
                {
                    for (int yOffset = 0; yOffset < 4; yOffset++)
                    {
                        Vector2I offsetPos = pos * 4 + new Vector2I(xOffset, yOffset);
                        if (_visionMap.VisionCells.ContainsKey(offsetPos))
                        {
                            numberOfSeenCells +=
                                _visionMap.GetVisionTimeOfCell(offsetPos) < 5.0f
                                    ? 1
                                    : 0; // Check if the cell has been seen for less than 5.0 seconds
                        }
                    }
                }

                if (!InfluenceCells.ContainsKey(pos))
                {
                    InfluenceCells[pos] = new InfluenceCell(0.0f, 0.0f);
                }

                InfluenceCells[pos].Confidence = numberOfSeenCells >= 4; // We want at least 4 cells to be visited to be confident at the influence cell
            }
        }
    }

}