using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;
using Jagara.Runtime.Gameplay;
using Jagara.Runtime.DungeonGen;

namespace Jagara.Tests.EditMode
{
    /// <summary>
    /// GridMover is largely MonoBehaviour/coroutine-lifecycle-bound (see CLAUDE.md guidance
    /// on not forcing Edit Mode tests onto tween logic), so these tests focus narrowly on the
    /// synchronous, pre-tween contract that the plan calls out explicitly: occupancy is
    /// checked/committed at the same synchronous point as CurrentCell - not deferred into the
    /// tween coroutine - and a blocked move must not mutate any state or consume a turn.
    /// </summary>
    public class GridMoverTests
    {
        private GameObject tilemapGO;
        private Tilemap tilemap;
        private GameObject moverGO;
        private GridMover mover;
        private OccupancyGrid occupancy;
        private FloorData floor;

        [SetUp]
        public void SetUp()
        {
            tilemapGO = new GameObject("Tilemap");
            tilemap = tilemapGO.AddComponent<Tilemap>();

            var grid = new TileType[3, 3];
            for (int x = 0; x < 3; x++)
            {
                for (int y = 0; y < 3; y++)
                {
                    grid[x, y] = TileType.Floor;
                }
            }

            floor = new FloorData(grid, new List<RoomData>());
            occupancy = new OccupancyGrid(3, 3);

            moverGO = new GameObject("Mover");
            mover = moverGO.AddComponent<GridMover>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(moverGO);
            Object.DestroyImmediate(tilemapGO);
        }

        [Test]
        public void Initialize_OccupiesStartingCell()
        {
            mover.Initialize(floor, tilemap, new Vector2Int(1, 1), occupancy);

            Assert.IsTrue(occupancy.IsOccupied(new Vector2Int(1, 1)));
        }

        [Test]
        public void TryMove_BlockedByOccupant_ReturnsFalseAndDoesNotCommitOrMove()
        {
            mover.Initialize(floor, tilemap, new Vector2Int(1, 1), occupancy);
            occupancy.Occupy(new Vector2Int(1, 2)); // simulate another entity standing north

            bool moved = mover.TryMove(Vector2Int.up);

            Assert.IsFalse(moved, "TryMove should fail when the target cell is occupied.");
            Assert.AreEqual(new Vector2Int(1, 1), mover.CurrentCell, "CurrentCell must not change on a blocked move.");
            Assert.IsFalse(mover.IsMoving, "A blocked move must not start a tween.");
            Assert.IsTrue(occupancy.IsOccupied(new Vector2Int(1, 1)), "The mover's own cell must remain occupied.");
            Assert.IsTrue(occupancy.IsOccupied(new Vector2Int(1, 2)), "The blocking occupant's cell must be untouched.");
        }

        [Test]
        public void TryMove_IntoWall_ReturnsFalseAndDoesNotCommitOrMove()
        {
            var wallGrid = new TileType[3, 3];
            for (int x = 0; x < 3; x++)
            {
                for (int y = 0; y < 3; y++)
                {
                    wallGrid[x, y] = TileType.Wall;
                }
            }
            wallGrid[1, 1] = TileType.Floor;
            floor = new FloorData(wallGrid, new List<RoomData>());

            mover.Initialize(floor, tilemap, new Vector2Int(1, 1), occupancy);

            bool moved = mover.TryMove(Vector2Int.up);

            Assert.IsFalse(moved);
            Assert.AreEqual(new Vector2Int(1, 1), mover.CurrentCell);
            Assert.IsFalse(mover.IsMoving);
        }

        [Test]
        public void TryMove_OpenAndUnoccupied_CommitsCellAndOccupancySynchronously()
        {
            mover.Initialize(floor, tilemap, new Vector2Int(1, 1), occupancy);

            bool moved = mover.TryMove(Vector2Int.up);

            Assert.IsTrue(moved);
            // Design decision #1: occupancy/CurrentCell commit synchronously at call time,
            // not in a tween-completion callback - assert this without waiting a frame.
            Assert.AreEqual(new Vector2Int(1, 2), mover.CurrentCell);
            Assert.IsFalse(occupancy.IsOccupied(new Vector2Int(1, 1)));
            Assert.IsTrue(occupancy.IsOccupied(new Vector2Int(1, 2)));
        }

        [Test]
        public void StepTo_SkipsValidation_CommitsCellAndOccupancySynchronously()
        {
            mover.Initialize(floor, tilemap, new Vector2Int(0, 0), occupancy);

            // No CanEnter check happens here - StepTo trusts the caller (a future enemy AI).
            mover.StepTo(new Vector2Int(0, 1));

            Assert.AreEqual(new Vector2Int(0, 1), mover.CurrentCell);
            Assert.IsFalse(occupancy.IsOccupied(new Vector2Int(0, 0)));
            Assert.IsTrue(occupancy.IsOccupied(new Vector2Int(0, 1)));
        }

        [Test]
        public void StepTo_WhileAlreadyMoving_LogsErrorAndDoesNotStartASecondTween()
        {
            mover.Initialize(floor, tilemap, new Vector2Int(1, 1), occupancy);
            mover.TryMove(Vector2Int.up); // starts a real tween; IsMoving is now true synchronously
            Assert.IsTrue(mover.IsMoving, "Precondition: a tween should be in flight.");

            LogAssert.Expect(LogType.Error, new Regex("StepTo called on .* while already moving"));
            mover.StepTo(new Vector2Int(2, 2));

            // The in-flight move's destination is untouched by the ignored StepTo call.
            Assert.AreEqual(new Vector2Int(1, 2), mover.CurrentCell);
            Assert.IsFalse(occupancy.IsOccupied(new Vector2Int(2, 2)));
        }

        [Test]
        public void MoveDuration_IsSettable()
        {
            mover.MoveDuration = 0.5f;

            Assert.AreEqual(0.5f, mover.MoveDuration);
        }
    }
}
