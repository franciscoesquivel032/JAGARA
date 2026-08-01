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

        // Parallel to `occupancy`: which GameObject occupies a cell, if known.
        // Kept separate (rather than replacing the bool grid) so existing
        // callers that only ever cared about "is this cell blocked" - and the
        // tests covering them - are unaffected by callers that also want to
        // know who's blocking it (e.g. bump-attack).
        private readonly GameObject[,] occupants;

        public OccupancyGrid(int width, int height)
        {
            occupancy = new bool[width, height];
            occupants = new GameObject[width, height];
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
        /// Marks the cell as occupied, optionally recording which GameObject
        /// occupies it (e.g. bump-attack target lookup). If already occupied,
        /// logs an error (this is a bug, not a valid state).
        /// </summary>
        public void Occupy(Vector2Int cell, GameObject occupant = null)
        {
            if (IsOccupied(cell))
            {
                Debug.LogError($"Attempted to occupy already-occupied cell {cell}");
                return;
            }

            if (cell.x >= 0 && cell.y >= 0 && cell.x < occupancy.GetLength(0) && cell.y < occupancy.GetLength(1))
            {
                occupancy[cell.x, cell.y] = true;
                occupants[cell.x, cell.y] = occupant;
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
                occupants[cell.x, cell.y] = null;
            }
        }

        /// <summary>
        /// Moves occupancy (and its recorded occupant, if any) from one cell to another.
        /// </summary>
        public void Move(Vector2Int from, Vector2Int to)
        {
            TryGetOccupant(from, out GameObject occupant);
            Vacate(from);
            Occupy(to, occupant);
        }

        /// <summary>
        /// Returns true and outputs the occupying GameObject if the cell is
        /// occupied AND an occupant was recorded (Occupy was called with a
        /// non-null occupant). Returns false (occupant = null) for an
        /// unoccupied cell, an out-of-bounds cell, or a cell occupied without
        /// an occupant reference (e.g. existing tests that only call Occupy(cell)).
        /// </summary>
        public bool TryGetOccupant(Vector2Int cell, out GameObject occupant)
        {
            if (!IsOccupied(cell))
            {
                occupant = null;
                return false;
            }

            occupant = occupants[cell.x, cell.y];
            return occupant != null;
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
