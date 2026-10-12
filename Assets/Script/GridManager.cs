
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Keeps track of the level grid and the cells that are blocked by walls or obstacles.
/// </summary>
public class GridManager : MonoBehaviour
{
    /// <summary>
    /// Size of each tile in world units.
    /// </summary>
    public float cellSize = 1f;

    /// <summary>
    /// Stores every grid coordinate that the robot cannot enter.
    /// </summary>
    private HashSet<Vector2Int> blockedCells =
        new HashSet<Vector2Int>();

    /// <summary>
    /// Converts a grid coordinate into its world-space position.
    /// </summary>
    public Vector3 GetWorldPosition(Vector2Int gridPosition)
    {
        return new Vector3(
            gridPosition.x * cellSize,
            transform.position.y,
            gridPosition.y * cellSize
        );
    }

    /// <summary>
    /// Converts a world-space position back into a grid cell.
    /// </summary>
    public Vector2Int GetGridPosition(Vector3 worldPosition)
    {
        int x = Mathf.RoundToInt(worldPosition.x / cellSize);
        int y = Mathf.RoundToInt(worldPosition.z / cellSize);

        return new Vector2Int(x, y);
    }

    /// <summary>
    /// Marks a cell as blocked so the robot cannot move through it.
    /// </summary>
    public void SetCellBlocked(Vector2Int position)
    {
        blockedCells.Add(position);
    }

    /// <summary>
    /// Returns true when a cell is currently blocked.
    /// </summary>
    public bool IsCellBlocked(Vector2Int position)
    {
        return blockedCells.Contains(position);
    }
    public void UnblockCell(Vector2Int position)
    {
        blockedCells.Remove(position);
    }
}
