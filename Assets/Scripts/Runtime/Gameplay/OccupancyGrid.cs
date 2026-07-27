using UnityEngine;
using Jagara.Runtime.DungeonGen;

namespace Jagara.Runtime.Gameplay
{
    /// <summary>
    /// Tracks which grid cells are currently occupied by entities.
    /// Used to prevent multiple entities from occupying the same cell.
    /// This is pure C# (no MonoBehaviour) and treats out-of-bounds cells as unoccupied.
    /// </summary>
    public class OccupancyGrid
    {
        private readonly bool[,] occupancy;

        public OccupancyGrid(int width, int height)
        {
            occupancy = new bool[width, height];
        }

        /// <summary>
        /// Returns true if the cell at (x, y) is currently occupied by an entity.
        /// Out-of-bounds cells return false (not an error; terrain checks handle bounds).
        /// </summary>
        public bool IsOccupied(Vector2Int cell)
        {
            if (cell.x < 0 || cell.y < 0 || cell.x >= occupancy.GetLength(0) || cell.y >= occupancy.GetLength(1))
            {
                return false;
            }

            return occupancy[cell.x, cell.y];
        }

        /// <summary>
        /// Marks the cell as occupied. If already occupied, logs an error (this is a bug, not a valid state).
        /// </summary>
        public void Occupy(Vector2Int cell)
        {
            if (IsOccupied(cell))
            {
                Debug.LogError($"Attempted to occupy already-occupied cell {cell}");
                return;
            }

            if (cell.x >= 0 && cell.y >= 0 && cell.x < occupancy.GetLength(0) && cell.y < occupancy.GetLength(1))
            {
                occupancy[cell.x, cell.y] = true;
            }
        }

        /// <summary>
        /// Marks the cell as unoccupied.
        /// </summary>
        public void Vacate(Vector2Int cell)
        {
            if (cell.x >= 0 && cell.y >= 0 && cell.x < occupancy.GetLength(0) && cell.y < occupancy.GetLength(1))
            {
                occupancy[cell.x, cell.y] = false;
            }
        }

        /// <summary>
        /// Moves occupancy from one cell to another (vacates from, then occupies to).
        /// </summary>
        public void Move(Vector2Int from, Vector2Int to)
        {
            Vacate(from);
            Occupy(to);
        }

        /// <summary>
        /// Returns true only when both the terrain allows passage (via TileVisualResolver.IsOpen)
        /// and the cell is unoccupied. This is the unified gameplay predicate for movement.
        /// </summary>
        public bool CanEnter(TileType[,] terrain, Vector2Int cell)
        {
            return TileVisualResolver.IsOpen(terrain, cell.x, cell.y) && !IsOccupied(cell);
        }
    }
}
