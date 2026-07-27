using NUnit.Framework;
using UnityEngine;
using Jagara.Runtime.Gameplay;
using Jagara.Runtime.DungeonGen;

namespace Jagara.Tests.EditMode
{
    public class DistanceFieldTests
    {
        private const TileType W = TileType.Wall;
        private const TileType F = TileType.Floor;

        [Test]
        public void GetDistance_AtOrigin_IsZero()
        {
            // Fully open 5x5 room.
            var terrain = new TileType[5, 5];
            for (int x = 0; x < 5; x++)
            {
                for (int y = 0; y < 5; y++)
                {
                    terrain[x, y] = F;
                }
            }

            var field = new DistanceField(5, 5);
            var origin = new Vector2Int(2, 2);
            field.Compute(terrain, origin);

            Assert.AreEqual(0, field.GetDistance(origin));
        }

        [Test]
        public void GetDistance_OpenRoom_MatchesManhattanDistance()
        {
            // Fully open 5x5 room: with no obstacles, BFS distance equals
            // Manhattan distance from the origin.
            var terrain = new TileType[5, 5];
            for (int x = 0; x < 5; x++)
            {
                for (int y = 0; y < 5; y++)
                {
                    terrain[x, y] = F;
                }
            }

            var field = new DistanceField(5, 5);
            var origin = new Vector2Int(0, 0);
            field.Compute(terrain, origin);

            Assert.AreEqual(0, field.GetDistance(new Vector2Int(0, 0)));
            Assert.AreEqual(1, field.GetDistance(new Vector2Int(1, 0)));
            Assert.AreEqual(1, field.GetDistance(new Vector2Int(0, 1)));
            Assert.AreEqual(2, field.GetDistance(new Vector2Int(1, 1)));
            Assert.AreEqual(4, field.GetDistance(new Vector2Int(4, 0)));
            Assert.AreEqual(8, field.GetDistance(new Vector2Int(4, 4)));
        }

        [Test]
        public void GetDistance_WallSegment_ForcesLongerPathAroundIt()
        {
            // Grid indexed [x, y], row printed at the bottom is y = 0.
            // A vertical wall spans x=2, y=0..3, leaving a single gap at
            // (2, 4) so the only route from the left side to the right side
            // detours up and over - a straight-line/Chebyshev distance would
            // undercount this badly.
            //
            // y=4:  F  F  F  F  F
            // y=3:  F  F  W  F  F
            // y=2:  F  F  W  F  F
            // y=1:  F  F  W  F  F
            // y=0:  F  F  W  F  F
            var terrain = new TileType[5, 5];
            for (int x = 0; x < 5; x++)
            {
                for (int y = 0; y < 5; y++)
                {
                    terrain[x, y] = F;
                }
            }

            for (int y = 0; y <= 3; y++)
            {
                terrain[2, y] = W;
            }

            var field = new DistanceField(5, 5);
            var origin = new Vector2Int(0, 0);
            field.Compute(terrain, origin);

            var target = new Vector2Int(4, 0);

            // Straight-line/Chebyshev distance from (0,0) to (4,0) is 4, but
            // the wall forces a detour up to y=4 and back down: (0,0)->(0,4)
            // is 4 steps, ->(4,4) is 4 more, ->(4,0) is 4 more = 12.
            Assert.AreEqual(12, field.GetDistance(target));
        }

        [Test]
        public void GetDistance_CellEnclosedByWalls_IsUnreachable()
        {
            // A single floor cell at (2, 2) is fully enclosed by walls on
            // all 4 cardinal sides, isolating it from the origin at (0, 0).
            var terrain = new TileType[5, 5];
            for (int x = 0; x < 5; x++)
            {
                for (int y = 0; y < 5; y++)
                {
                    terrain[x, y] = F;
                }
            }

            terrain[1, 2] = W;
            terrain[3, 2] = W;
            terrain[2, 1] = W;
            terrain[2, 3] = W;

            var field = new DistanceField(5, 5);
            field.Compute(terrain, new Vector2Int(0, 0));

            Assert.AreEqual(DistanceField.Unreachable, field.GetDistance(new Vector2Int(2, 2)));
        }

        [Test]
        public void GetDistance_OutOfBounds_ReturnsUnreachable_DoesNotThrow()
        {
            var terrain = new TileType[5, 5];
            for (int x = 0; x < 5; x++)
            {
                for (int y = 0; y < 5; y++)
                {
                    terrain[x, y] = F;
                }
            }

            var field = new DistanceField(5, 5);
            field.Compute(terrain, new Vector2Int(2, 2));

            Assert.DoesNotThrow(() =>
            {
                Assert.AreEqual(DistanceField.Unreachable, field.GetDistance(new Vector2Int(-1, 2)));
                Assert.AreEqual(DistanceField.Unreachable, field.GetDistance(new Vector2Int(2, -1)));
                Assert.AreEqual(DistanceField.Unreachable, field.GetDistance(new Vector2Int(5, 2)));
                Assert.AreEqual(DistanceField.Unreachable, field.GetDistance(new Vector2Int(2, 5)));
                Assert.AreEqual(DistanceField.Unreachable, field.GetDistance(new Vector2Int(-5, -5)));
            });
        }

        [Test]
        public void Compute_OriginOutOfBounds_LeavesEveryCellUnreachable()
        {
            var terrain = new TileType[5, 5];
            for (int x = 0; x < 5; x++)
            {
                for (int y = 0; y < 5; y++)
                {
                    terrain[x, y] = F;
                }
            }

            var field = new DistanceField(5, 5);

            Assert.DoesNotThrow(() => field.Compute(terrain, new Vector2Int(-1, -1)));

            for (int x = 0; x < 5; x++)
            {
                for (int y = 0; y < 5; y++)
                {
                    Assert.AreEqual(DistanceField.Unreachable, field.GetDistance(new Vector2Int(x, y)));
                }
            }
        }

        [Test]
        public void Compute_CalledTwiceWithDifferentOrigins_ProducesFreshResultsEachTime()
        {
            // Same wall segment as the detour test above, splitting the grid
            // into a left region (x=0..1) and right region (x=3..4) joined
            // only through the gap at y=4.
            var terrain = new TileType[5, 5];
            for (int x = 0; x < 5; x++)
            {
                for (int y = 0; y < 5; y++)
                {
                    terrain[x, y] = F;
                }
            }

            for (int y = 0; y <= 3; y++)
            {
                terrain[2, y] = W;
            }

            var field = new DistanceField(5, 5);

            // First call: origin on the left side.
            field.Compute(terrain, new Vector2Int(0, 0));
            Assert.AreEqual(0, field.GetDistance(new Vector2Int(0, 0)));
            Assert.AreEqual(12, field.GetDistance(new Vector2Int(4, 0)));

            // Second call: origin on the right side. The distance to the
            // former origin must reflect only this call's BFS, not leak the
            // previous call's distance of 0.
            field.Compute(terrain, new Vector2Int(4, 0));
            Assert.AreEqual(0, field.GetDistance(new Vector2Int(4, 0)));
            Assert.AreEqual(12, field.GetDistance(new Vector2Int(0, 0)));

            // Third call: a smaller, fully open region so a previously
            // reachable cell becomes unreachable and must not retain its
            // old finite distance.
            var openTerrain = new TileType[5, 5];
            for (int x = 0; x < 5; x++)
            {
                for (int y = 0; y < 5; y++)
                {
                    openTerrain[x, y] = W;
                }
            }
            openTerrain[0, 0] = F;

            field.Compute(openTerrain, new Vector2Int(0, 0));
            Assert.AreEqual(0, field.GetDistance(new Vector2Int(0, 0)));
            Assert.AreEqual(DistanceField.Unreachable, field.GetDistance(new Vector2Int(4, 0)));
        }
    }
}
