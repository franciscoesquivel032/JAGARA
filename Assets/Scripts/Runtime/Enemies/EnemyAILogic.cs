using System;
using System.Collections.Generic;
using UnityEngine;
using Jagara.Runtime.DungeonGen;
using Jagara.Runtime.Gameplay;

namespace Jagara.Runtime.Enemies
{
    /// <summary>
    /// Static, pure enemy AI logic: wake/forget detection and single-step
    /// chase movement. Contains no MonoBehaviour state and no per-enemy
    /// config - callers (a later task's EnemyController) supply N/M and the
    /// shared <see cref="DistanceField"/>/<see cref="OccupancyGrid"/> each turn.
    /// No LINQ and no per-call heap allocations, since this runs per-enemy,
    /// per-turn.
    /// </summary>
    public static class EnemyAILogic
    {
        /// <summary>
        /// Returns true if the enemy should transition Dormant -> Chasing this
        /// turn: either <paramref name="enemyCell"/> and <paramref name="playerCell"/>
        /// are both inside the same room in <paramref name="rooms"/>, or their
        /// Manhattan distance is at most <paramref name="detectionRadius"/>.
        /// </summary>
        public static bool ShouldWake(
            Vector2Int enemyCell,
            Vector2Int playerCell,
            IReadOnlyList<RoomData> rooms,
            int detectionRadius)
        {
            for (int i = 0; i < rooms.Count; i++)
            {
                RoomData room = rooms[i];
                if (room.Contains(enemyCell.x, enemyCell.y) && room.Contains(playerCell.x, playerCell.y))
                {
                    return true;
                }
            }

            return ManhattanDistance(enemyCell, playerCell) <= detectionRadius;
        }

        /// <summary>
        /// Returns true if a Chasing enemy should transition back to Dormant
        /// this turn: the player has moved strictly farther than
        /// <paramref name="forgetRadius"/> tiles away (Manhattan distance).
        /// Equal to <paramref name="forgetRadius"/> does NOT forget.
        /// </summary>
        public static bool ShouldForget(Vector2Int enemyCell, Vector2Int playerCell, int forgetRadius)
        {
            return ManhattanDistance(enemyCell, playerCell) > forgetRadius;
        }

        /// <summary>
        /// Returns true if <paramref name="a"/> and <paramref name="b"/> are
        /// cardinally adjacent (Manhattan distance exactly 1).
        /// </summary>
        public static bool IsAdjacent(Vector2Int a, Vector2Int b)
        {
            return ManhattanDistance(a, b) == 1;
        }

        /// <summary>
        /// Picks the enemy's next single-step move toward the player, using
        /// <paramref name="distances"/> (a <see cref="DistanceField"/> computed
        /// from the player's cell) plus the current <paramref name="occupancy"/>
        /// and <paramref name="terrain"/>.
        /// Two-pass, fixed North/East/South/West probe order:
        /// Pass 1 (advance) returns the first enterable neighbor strictly closer
        /// to the player than <paramref name="currentCell"/>. If none exists,
        /// Pass 2 (sidestep) returns the first enterable neighbor at exactly the
        /// same distance (a lateral move around a blocker). If neither pass
        /// finds a candidate, the enemy waits: <paramref name="step"/> is set to
        /// <paramref name="currentCell"/> and this returns false.
        /// This is deliberately deterministic (fixed probe order, no "any
        /// minimum" search) so multiple enemies resolved in the same turn
        /// produce reproducible, non-thrashing movement.
        /// </summary>
        public static bool TryChooseStep(
            DistanceField distances,
            OccupancyGrid occupancy,
            TileType[,] terrain,
            Vector2Int currentCell,
            out Vector2Int step)
        {
            int currentDistance = distances.GetDistance(currentCell);

            Vector2Int north = new Vector2Int(currentCell.x, currentCell.y + 1);
            Vector2Int east = new Vector2Int(currentCell.x + 1, currentCell.y);
            Vector2Int south = new Vector2Int(currentCell.x, currentCell.y - 1);
            Vector2Int west = new Vector2Int(currentCell.x - 1, currentCell.y);

            // Pass 1: advance - first neighbor that is strictly closer to the player.
            if (occupancy.CanEnter(terrain, north) && distances.GetDistance(north) < currentDistance)
            {
                step = north;
                return true;
            }

            if (occupancy.CanEnter(terrain, east) && distances.GetDistance(east) < currentDistance)
            {
                step = east;
                return true;
            }

            if (occupancy.CanEnter(terrain, south) && distances.GetDistance(south) < currentDistance)
            {
                step = south;
                return true;
            }

            if (occupancy.CanEnter(terrain, west) && distances.GetDistance(west) < currentDistance)
            {
                step = west;
                return true;
            }

            // Pass 2: sidestep - first neighbor at exactly the same distance.
            if (occupancy.CanEnter(terrain, north) && distances.GetDistance(north) == currentDistance)
            {
                step = north;
                return true;
            }

            if (occupancy.CanEnter(terrain, east) && distances.GetDistance(east) == currentDistance)
            {
                step = east;
                return true;
            }

            if (occupancy.CanEnter(terrain, south) && distances.GetDistance(south) == currentDistance)
            {
                step = south;
                return true;
            }

            if (occupancy.CanEnter(terrain, west) && distances.GetDistance(west) == currentDistance)
            {
                step = west;
                return true;
            }

            // Wait: no advance or sidestep candidate available.
            step = currentCell;
            return false;
        }

        private static int ManhattanDistance(Vector2Int a, Vector2Int b)
        {
            return Math.Abs(a.x - b.x) + Math.Abs(a.y - b.y);
        }
    }
}
