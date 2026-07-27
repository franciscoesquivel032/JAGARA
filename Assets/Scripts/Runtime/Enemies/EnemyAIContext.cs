using System.Collections.Generic;
using UnityEngine;
using Jagara.Runtime.DungeonGen;
using Jagara.Runtime.Gameplay;
using Jagara.Runtime.TurnSystem;

namespace Jagara.Runtime.Enemies
{
    /// <summary>
    /// Pure C#, one instance shared by every EnemyController on a floor.
    /// Bundles the read-only floor/occupancy state enemies need each turn,
    /// plus a lazily-recomputed BFS DistanceField from the player's cell.
    /// Distances only recomputes the first time it is read in a given turn
    /// (tracked via TurnResolver.TurnNumber) - a floor where every enemy is
    /// Dormant (and therefore never reads Distances) never pays for a BFS.
    /// </summary>
    public class EnemyAIContext
    {
        // Sentinel that can never equal a real TurnResolver.TurnNumber (which
        // starts at 0 and only increments), guaranteeing the first read of
        // Distances always computes.
        private const int NeverComputedTurn = -1;

        private readonly FloorData floor;
        private readonly OccupancyGrid occupancy;
        private readonly TurnResolver resolver;
        private readonly GridMover playerMover;
        private readonly DistanceField distanceField;

        private int lastComputedTurn = NeverComputedTurn;

        public EnemyAIContext(FloorData floor, OccupancyGrid occupancy, TurnResolver resolver, GridMover playerMover)
        {
            this.floor = floor;
            this.occupancy = occupancy;
            this.resolver = resolver;
            this.playerMover = playerMover;

            int width = floor.Grid.GetLength(0);
            int height = floor.Grid.GetLength(1);
            distanceField = new DistanceField(width, height);
        }

        /// <summary>The player's current grid cell.</summary>
        public Vector2Int PlayerCell => playerMover.CurrentCell;

        /// <summary>The floor's terrain grid, for EnemyAILogic.TryChooseStep.</summary>
        public TileType[,] Terrain => floor.Grid;

        /// <summary>The floor's rooms, for EnemyAILogic.ShouldWake's same-room check.</summary>
        public IReadOnlyList<RoomData> Rooms => floor.Rooms;

        /// <summary>The shared occupancy grid, for EnemyAILogic.TryChooseStep.</summary>
        public OccupancyGrid Occupancy => occupancy;

        /// <summary>
        /// The shared BFS distance field from the player's current cell,
        /// recomputed at most once per turn (on first access after
        /// TurnResolver.TurnNumber advances). Only read this when actually
        /// about to path with it - a Dormant or player-adjacent enemy should
        /// never touch this getter, or the "sleeping floor" case still pays
        /// for the BFS.
        /// </summary>
        public DistanceField Distances
        {
            get
            {
                if (lastComputedTurn != resolver.TurnNumber)
                {
                    distanceField.Compute(floor.Grid, PlayerCell);
                    lastComputedTurn = resolver.TurnNumber;
                }

                return distanceField;
            }
        }
    }
}
