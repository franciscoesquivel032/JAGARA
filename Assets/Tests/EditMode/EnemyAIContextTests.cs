using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;
using Jagara.Runtime.DungeonGen;
using Jagara.Runtime.Gameplay;
using Jagara.Runtime.TurnSystem;
using Jagara.Runtime.Enemies;

namespace Jagara.Tests.EditMode
{
    /// <summary>
    /// EnemyAIContext.Distances is meant to recompute its BFS at most once
    /// per turn (see the class doc), so a floor where every enemy stays
    /// Dormant never pays for a BFS. DistanceField doesn't expose a way to
    /// directly observe "was Compute called", so these tests prove it
    /// indirectly: mutate the terrain array (shared by reference between the
    /// FloorData handed to the context and the test) between two accesses,
    /// then check whether the returned distances reflect the mutation
    /// (recomputed) or not (still cached from the first access).
    /// </summary>
    public class EnemyAIContextTests
    {
        private const TileType W = TileType.Wall;
        private const TileType F = TileType.Floor;

        private GameObject tilemapGO;
        private Tilemap tilemap;
        private GameObject playerMoverGO;
        private GridMover playerMover;
        private OccupancyGrid occupancy;
        private FloorData floor;
        private TileType[,] terrain;
        private TurnResolver resolver;
        private EnemyAIContext context;

        [SetUp]
        public void SetUp()
        {
            // 5x1 open corridor; player starts at the west end (0,0).
            terrain = new TileType[5, 1] { { F }, { F }, { F }, { F }, { F } };
            floor = new FloorData(terrain, new List<RoomData>());
            occupancy = new OccupancyGrid(5, 1);

            tilemapGO = new GameObject("Tilemap");
            tilemap = tilemapGO.AddComponent<Tilemap>();

            playerMoverGO = new GameObject("PlayerMover");
            playerMover = playerMoverGO.AddComponent<GridMover>();
            playerMover.Initialize(floor, tilemap, new Vector2Int(0, 0), occupancy);

            resolver = new TurnResolver();
            context = new EnemyAIContext(floor, occupancy, resolver, playerMover);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(playerMoverGO);
            Object.DestroyImmediate(tilemapGO);
        }

        [Test]
        public void Distances_AccessedTwiceWithinSameTurn_DoesNotRecompute_ReflectsStaleTerrain()
        {
            // First access computes the BFS over the fully-open corridor:
            // (4,0) is 4 steps from the player's origin at (0,0).
            Assert.AreEqual(4, context.Distances.GetDistance(new Vector2Int(4, 0)));

            // Seal the corridor after the first access, still within the same
            // turn (TurnNumber has not advanced). If Distances recomputed,
            // (4,0) would become Unreachable (a 1-wide corridor blocked
            // outright); if it's still serving the cached field, it will
            // keep reporting the pre-wall distance of 4.
            terrain[2, 0] = W;

            Assert.AreEqual(4, context.Distances.GetDistance(new Vector2Int(4, 0)),
                "Distances must not recompute on a second access within the same turn.");
        }

        [Test]
        public void Distances_AccessedAfterTurnAdvances_Recomputes_ReflectsFreshTerrain()
        {
            // Prime the cache for turn 0 over the open corridor.
            Assert.AreEqual(4, context.Distances.GetDistance(new Vector2Int(4, 0)));

            // Seal the corridor, then advance the turn.
            terrain[2, 0] = W;
            resolver.EndPlayerTurn();

            // A fresh access after the turn advanced must recompute and see
            // the wall: (4,0) is now cut off from the player's origin.
            Assert.AreEqual(DistanceField.Unreachable, context.Distances.GetDistance(new Vector2Int(4, 0)),
                "Distances must recompute once TurnResolver.TurnNumber has advanced.");
        }

        [Test]
        public void Distances_ReflectsPlayerCell_AsBfsOrigin()
        {
            Assert.AreEqual(new Vector2Int(0, 0), context.PlayerCell);
            Assert.AreEqual(0, context.Distances.GetDistance(context.PlayerCell));
        }
    }
}
