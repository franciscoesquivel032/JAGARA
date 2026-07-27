using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Jagara.Runtime.DungeonGen;
using Jagara.Runtime.Gameplay;
using Jagara.Runtime.Enemies;

namespace Jagara.Tests.EditMode
{
    public class EnemyAILogicTests
    {
        private const TileType W = TileType.Wall;
        private const TileType F = TileType.Floor;

        // ---- ShouldWake ----

        [Test]
        public void ShouldWake_WakesByRadius_WhenInDifferentRoomsButWithinN()
        {
            var enemyCell = new Vector2Int(0, 0);
            var playerCell = new Vector2Int(3, 0);

            // Two disjoint 2x2 rooms; neither cell falls in the other's room,
            // so only the radius clause can fire.
            var roomA = new RoomData(0, 0, 2, 2);
            var roomB = new RoomData(3, 0, 2, 2);
            var rooms = new List<RoomData> { roomA, roomB };

            Assert.IsFalse(roomA.Contains(playerCell.x, playerCell.y));
            Assert.IsFalse(roomB.Contains(enemyCell.x, enemyCell.y));

            // Manhattan distance is 3.
            Assert.IsTrue(EnemyAILogic.ShouldWake(enemyCell, playerCell, rooms, 3));
        }

        [Test]
        public void ShouldWake_DoesNotWake_WhenDifferentRoomsAndBeyondRadius()
        {
            var enemyCell = new Vector2Int(0, 0);
            var playerCell = new Vector2Int(10, 10);

            var roomA = new RoomData(0, 0, 2, 2);
            var rooms = new List<RoomData> { roomA };

            Assert.IsFalse(EnemyAILogic.ShouldWake(enemyCell, playerCell, rooms, 3));
        }

        [Test]
        public void ShouldWake_WakesBySameRoom_EvenWhenBeyondRadius()
        {
            var enemyCell = new Vector2Int(0, 0);
            var playerCell = new Vector2Int(1, 1);

            // Manhattan distance between the two cells is 2.
            var bigRoom = new RoomData(0, 0, 5, 5);
            var rooms = new List<RoomData> { bigRoom };

            // Radius alone (N=1) would not wake the enemy...
            Assert.IsFalse(new Vector2Int(1, 1) == enemyCell); // sanity: cells differ
            Assert.IsTrue(bigRoom.Contains(enemyCell.x, enemyCell.y));
            Assert.IsTrue(bigRoom.Contains(playerCell.x, playerCell.y));

            // ...but the same-room clause must still wake it.
            Assert.IsTrue(EnemyAILogic.ShouldWake(enemyCell, playerCell, rooms, 1));
        }

        // ---- ShouldForget ----

        [Test]
        public void ShouldForget_DoesNotForget_WhenDistanceEqualsM()
        {
            var enemyCell = new Vector2Int(0, 0);
            var playerCell = new Vector2Int(3, 0); // Manhattan distance 3.

            Assert.IsFalse(EnemyAILogic.ShouldForget(enemyCell, playerCell, 3));
        }

        [Test]
        public void ShouldForget_Forgets_WhenDistanceIsMPlusOne()
        {
            var enemyCell = new Vector2Int(0, 0);
            var playerCell = new Vector2Int(3, 0); // Manhattan distance 3 = M(2) + 1.

            Assert.IsTrue(EnemyAILogic.ShouldForget(enemyCell, playerCell, 2));
        }

        // ---- IsAdjacent ----

        [Test]
        public void IsAdjacent_ReturnsTrue_ForCardinalNeighbor()
        {
            Assert.IsTrue(EnemyAILogic.IsAdjacent(new Vector2Int(2, 2), new Vector2Int(3, 2)));
        }

        [Test]
        public void IsAdjacent_ReturnsFalse_ForSameCellAndForDiagonal()
        {
            Assert.IsFalse(EnemyAILogic.IsAdjacent(new Vector2Int(2, 2), new Vector2Int(2, 2)));
            // Manhattan distance 2, not cardinally adjacent.
            Assert.IsFalse(EnemyAILogic.IsAdjacent(new Vector2Int(2, 2), new Vector2Int(3, 3)));
        }

        // ---- TryChooseStep ----

        [Test]
        public void TryChooseStep_Advance_PicksFirstStrictlyCloserNeighbor_InFixedProbeOrder()
        {
            // 5x3 grid, all floor except a wall at (2,1) splitting the middle
            // row. Player (origin) sits at (4,1); the enemy at (1,1) is
            // equidistant (via BFS) going around the wall through either
            // (1,2) [North] or (1,0) [South] - both at distance 4, one less
            // than the enemy's distance of 5. East, (2,1), is a wall.
            // This gives two equally valid "advance" candidates, so only the
            // fixed N,E,S,W probe order can make the choice deterministic.
            var terrain = new TileType[5, 3];
            for (int x = 0; x < 5; x++)
            {
                for (int y = 0; y < 3; y++)
                {
                    terrain[x, y] = F;
                }
            }

            terrain[2, 1] = W;

            var distances = new DistanceField(5, 3);
            distances.Compute(terrain, new Vector2Int(4, 1));

            var enemyCell = new Vector2Int(1, 1);
            Assert.AreEqual(5, distances.GetDistance(enemyCell));
            Assert.AreEqual(4, distances.GetDistance(new Vector2Int(1, 2)));
            Assert.AreEqual(4, distances.GetDistance(new Vector2Int(1, 0)));

            var occupancy = new OccupancyGrid(5, 3);

            bool moved = EnemyAILogic.TryChooseStep(distances, occupancy, terrain, enemyCell, out Vector2Int step);
            Assert.IsTrue(moved);
            Assert.AreEqual(new Vector2Int(1, 2), step); // North wins over South.

            // Pure function - repeated calls with the same inputs must agree.
            bool movedAgain = EnemyAILogic.TryChooseStep(distances, occupancy, terrain, enemyCell, out Vector2Int stepAgain);
            Assert.IsTrue(movedAgain);
            Assert.AreEqual(new Vector2Int(1, 2), stepAgain);
        }

        [Test]
        public void TryChooseStep_Sidestep_SkipsOccupiedTiedNeighbor_PicksNextInProbeOrder()
        {
            // Grid-adjacent, BFS-reachable cells can never share the exact
            // same distance (a standard bipartite-graph property of 4-way
            // grid BFS: every step changes distance by exactly +-1). So the
            // only way for the "sidestep" pass (equal distance) to have a
            // real candidate is when the enemy's own cell is Unreachable -
            // e.g. stuck in a pocket fully sealed off from the player - in
            // which case every open neighbor in that same pocket is also
            // Unreachable, tying with the enemy's own (Unreachable) distance.
            //
            // Wall column at x=1 seals off the right-hand pocket (x=2..4)
            // from the player at (0,1). The enemy sits at (3,1) in the
            // middle of that pocket, with open, tied (Unreachable) neighbors
            // on all four sides.
            var terrain = new TileType[5, 3];
            for (int x = 0; x < 5; x++)
            {
                for (int y = 0; y < 3; y++)
                {
                    terrain[x, y] = x == 1 ? W : F;
                }
            }

            var distances = new DistanceField(5, 3);
            distances.Compute(terrain, new Vector2Int(0, 1));

            var enemyCell = new Vector2Int(3, 1);
            Assert.AreEqual(DistanceField.Unreachable, distances.GetDistance(enemyCell));
            Assert.AreEqual(DistanceField.Unreachable, distances.GetDistance(new Vector2Int(3, 2))); // North
            Assert.AreEqual(DistanceField.Unreachable, distances.GetDistance(new Vector2Int(4, 1))); // East

            var occupancy = new OccupancyGrid(5, 3);
            occupancy.Occupy(new Vector2Int(3, 2)); // Occupy North, first in probe order.

            bool moved = EnemyAILogic.TryChooseStep(distances, occupancy, terrain, enemyCell, out Vector2Int step);

            Assert.IsTrue(moved);
            Assert.AreEqual(new Vector2Int(4, 1), step); // East: next tied candidate in N,E,S,W order.
        }

        [Test]
        public void TryChooseStep_Waits_WhenOnlyForwardNeighborIsOccupiedAndRestAreOutOfBounds()
        {
            // 1-tall, 3-wide corridor. Player at x=0, enemy at x=2 (distance
            // 2). The only neighbor that could advance is West (x=1,
            // distance 1); North/East/South are all out of bounds. With West
            // occupied by another entity, there is no advance and no
            // sidestep candidate (an occupied cell is never a candidate, and
            // out-of-bounds cells fail CanEnter outright) - the enemy must
            // wait.
            var terrain = new TileType[3, 1] { { F }, { F }, { F } };

            var distances = new DistanceField(3, 1);
            distances.Compute(terrain, new Vector2Int(0, 0));

            var enemyCell = new Vector2Int(2, 0);
            Assert.AreEqual(2, distances.GetDistance(enemyCell));

            var occupancy = new OccupancyGrid(3, 1);
            occupancy.Occupy(new Vector2Int(1, 0));

            bool moved = EnemyAILogic.TryChooseStep(distances, occupancy, terrain, enemyCell, out Vector2Int step);

            Assert.IsFalse(moved);
            Assert.AreEqual(enemyCell, step);
        }

        // ---- RoomData.Contains ----

        [Test]
        public void RoomDataContains_TrueForCellsInsideBounds_FalseForCellsOutside()
        {
            var room = new RoomData(2, 3, 4, 2); // covers x:2..5, y:3..4 (Right=6, Bottom=5).

            Assert.IsTrue(room.Contains(2, 3));  // top-left corner, inclusive.
            Assert.IsTrue(room.Contains(5, 4));  // bottom-right-most cell, inclusive.

            Assert.IsFalse(room.Contains(6, 3)); // == Right, exclusive.
            Assert.IsFalse(room.Contains(2, 5)); // == Bottom, exclusive.
            Assert.IsFalse(room.Contains(1, 3)); // left of the room.
            Assert.IsFalse(room.Contains(2, 2)); // above the room.
        }
    }
}
