using System.Collections.Generic;
using UnityEngine;
using Jagara.Runtime.DungeonGen;

namespace Jagara.Runtime.Gameplay
{
    /// <summary>
    /// Single BFS distance field (4-directional, terrain-only) from an origin
    /// cell, shared by all enemies for pathfinding. Computed once per turn from
    /// the player's cell. Pure C# (no MonoBehaviour) so it can run in Edit Mode
    /// tests. Walkability is delegated to <see cref="TileVisualResolver.IsOpen"/>
    /// (terrain alone - occupancy is layered in separately by step-selection
    /// logic, not baked into this field).
    /// All buffers (distance grid and BFS frontier) are preallocated in the
    /// constructor and reused by every <see cref="Compute"/> call, so repeated
    /// per-turn computation over ~2500-cell grids does not allocate.
    /// </summary>
    public class DistanceField
    {
        public const int Unreachable = int.MaxValue;

        private readonly int width;
        private readonly int height;
        private readonly int[,] distances;

        // Preallocated with capacity for every cell so Enqueue/Clear during
        // Compute() never grows the backing array (Clear() only resets the
        // head/tail/count indices, it does not release the array).
        private readonly Queue<Vector2Int> frontier;

        public DistanceField(int width, int height)
        {
            this.width = width;
            this.height = height;
            distances = new int[width, height];
            frontier = new Queue<Vector2Int>(width * height);
        }

        /// <summary>
        /// Runs a 4-neighbor BFS from <paramref name="origin"/> over
        /// <paramref name="terrain"/>, overwriting all previously computed
        /// distances. Cells unreached by this call end up <see cref="Unreachable"/>,
        /// even if a prior Compute call had reached them. If origin is
        /// out-of-bounds, every cell is left Unreachable.
        /// </summary>
        public void Compute(TileType[,] terrain, Vector2Int origin)
        {
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    distances[x, y] = Unreachable;
                }
            }

            frontier.Clear();

            if (origin.x < 0 || origin.y < 0 || origin.x >= width || origin.y >= height)
            {
                return;
            }

            distances[origin.x, origin.y] = 0;
            frontier.Enqueue(origin);

            while (frontier.Count > 0)
            {
                Vector2Int current = frontier.Dequeue();
                int nextDistance = distances[current.x, current.y] + 1;

                TryVisit(terrain, current.x, current.y + 1, nextDistance);
                TryVisit(terrain, current.x + 1, current.y, nextDistance);
                TryVisit(terrain, current.x, current.y - 1, nextDistance);
                TryVisit(terrain, current.x - 1, current.y, nextDistance);
            }
        }

        private void TryVisit(TileType[,] terrain, int x, int y, int distance)
        {
            if (x < 0 || y < 0 || x >= width || y >= height)
            {
                return;
            }

            if (distances[x, y] != Unreachable)
            {
                return;
            }

            if (!TileVisualResolver.IsOpen(terrain, x, y))
            {
                return;
            }

            distances[x, y] = distance;
            frontier.Enqueue(new Vector2Int(x, y));
        }

        /// <summary>
        /// Returns the BFS distance from the last <see cref="Compute"/> call's
        /// origin to <paramref name="cell"/>. Returns <see cref="Unreachable"/>
        /// for cells never reached by the BFS and for out-of-bounds cells; never
        /// throws.
        /// </summary>
        public int GetDistance(Vector2Int cell)
        {
            if (cell.x < 0 || cell.y < 0 || cell.x >= width || cell.y >= height)
            {
                return Unreachable;
            }

            return distances[cell.x, cell.y];
        }
    }
}
