using NUnit.Framework;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.TestTools;
using Jagara.Runtime.Gameplay;
using Jagara.Runtime.DungeonGen;

namespace Jagara.Tests.EditMode
{
    public class OccupancyGridTests
    {
        private OccupancyGrid grid;

        [SetUp]
        public void SetUp()
        {
            grid = new OccupancyGrid(10, 10);
        }

        [Test]
        public void IsOccupied_ReturnsFalse_ForUnoccupiedCell()
        {
            Assert.IsFalse(grid.IsOccupied(new Vector2Int(5, 5)));
        }

        [Test]
        public void Occupy_MarksCellAsOccupied()
        {
            var cell = new Vector2Int(3, 4);
            grid.Occupy(cell);
            Assert.IsTrue(grid.IsOccupied(cell));
        }

        [Test]
        public void Vacate_MarksCellAsUnoccupied()
        {
            var cell = new Vector2Int(2, 3);
            grid.Occupy(cell);
            Assert.IsTrue(grid.IsOccupied(cell));

            grid.Vacate(cell);
            Assert.IsFalse(grid.IsOccupied(cell));
        }

        [Test]
        public void Occupy_DoubleOccupy_LogsError()
        {
            var cell = new Vector2Int(1, 1);
            grid.Occupy(cell);

            LogAssert.Expect(LogType.Error, new Regex("Attempted to occupy already-occupied cell"));
            grid.Occupy(cell);
        }

        [Test]
        public void Move_VacatesSourceAndOccupiesDestination()
        {
            var from = new Vector2Int(2, 2);
            var to = new Vector2Int(5, 5);

            grid.Occupy(from);
            Assert.IsTrue(grid.IsOccupied(from));
            Assert.IsFalse(grid.IsOccupied(to));

            grid.Move(from, to);

            Assert.IsFalse(grid.IsOccupied(from));
            Assert.IsTrue(grid.IsOccupied(to));
        }

        [Test]
        public void IsOccupied_OutOfBounds_ReturnsFalse()
        {
            Assert.IsFalse(grid.IsOccupied(new Vector2Int(-1, 5)));
            Assert.IsFalse(grid.IsOccupied(new Vector2Int(5, -1)));
            Assert.IsFalse(grid.IsOccupied(new Vector2Int(10, 5)));
            Assert.IsFalse(grid.IsOccupied(new Vector2Int(5, 10)));
            Assert.IsFalse(grid.IsOccupied(new Vector2Int(-5, -5)));
            Assert.IsFalse(grid.IsOccupied(new Vector2Int(15, 15)));
        }

        [Test]
        public void Occupy_OutOfBounds_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => grid.Occupy(new Vector2Int(-1, 5)));
            Assert.DoesNotThrow(() => grid.Occupy(new Vector2Int(5, -1)));
            Assert.DoesNotThrow(() => grid.Occupy(new Vector2Int(10, 5)));
            Assert.DoesNotThrow(() => grid.Occupy(new Vector2Int(5, 10)));
        }

        [Test]
        public void Vacate_OutOfBounds_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => grid.Vacate(new Vector2Int(-1, 5)));
            Assert.DoesNotThrow(() => grid.Vacate(new Vector2Int(5, -1)));
            Assert.DoesNotThrow(() => grid.Vacate(new Vector2Int(10, 5)));
            Assert.DoesNotThrow(() => grid.Vacate(new Vector2Int(5, 10)));
        }

        [Test]
        public void CanEnter_ReturnsFalse_WhenCellIsOccupied()
        {
            var terrain = new TileType[,]
            {
                { TileType.Wall, TileType.Wall, TileType.Wall },
                { TileType.Wall, TileType.Floor, TileType.Wall },
                { TileType.Wall, TileType.Wall, TileType.Wall }
            };

            var cell = new Vector2Int(1, 1);
            grid = new OccupancyGrid(3, 3);
            grid.Occupy(cell);

            Assert.IsFalse(grid.CanEnter(terrain, cell));
        }

        [Test]
        public void CanEnter_ReturnsFalse_WhenTerrainIsWall()
        {
            var terrain = new TileType[,]
            {
                { TileType.Wall, TileType.Wall, TileType.Wall },
                { TileType.Wall, TileType.Wall, TileType.Wall },
                { TileType.Wall, TileType.Wall, TileType.Wall }
            };

            grid = new OccupancyGrid(3, 3);
            var cell = new Vector2Int(1, 1);

            Assert.IsFalse(grid.CanEnter(terrain, cell));
        }

        [Test]
        public void CanEnter_ReturnsTrue_WhenTerrainIsOpenAndUnoccupied()
        {
            var terrain = new TileType[,]
            {
                { TileType.Wall, TileType.Wall, TileType.Wall },
                { TileType.Wall, TileType.Floor, TileType.Wall },
                { TileType.Wall, TileType.Wall, TileType.Wall }
            };

            grid = new OccupancyGrid(3, 3);
            var cell = new Vector2Int(1, 1);

            Assert.IsTrue(grid.CanEnter(terrain, cell));
        }

        [Test]
        public void CanEnter_ReturnsFalse_WhenTerrainIsOutOfBounds()
        {
            var terrain = new TileType[,]
            {
                { TileType.Floor }
            };

            grid = new OccupancyGrid(1, 1);
            var cell = new Vector2Int(5, 5);

            Assert.IsFalse(grid.CanEnter(terrain, cell));
        }

        [Test]
        public void CanEnter_AcceptsAllWalkableTerrainTypes()
        {
            // Create a 3-wide, 1-tall terrain (grid[width, height])
            var terrainFloor = new TileType[3, 1] { { TileType.Floor }, { TileType.Floor }, { TileType.Floor } };
            var terrainCorridor = new TileType[3, 1] { { TileType.Corridor }, { TileType.Corridor }, { TileType.Corridor } };
            var terrainStairs = new TileType[3, 1] { { TileType.StairsDown }, { TileType.StairsDown }, { TileType.StairsDown } };

            grid = new OccupancyGrid(3, 1);

            Assert.IsTrue(grid.CanEnter(terrainFloor, new Vector2Int(1, 0)));
            Assert.IsTrue(grid.CanEnter(terrainCorridor, new Vector2Int(1, 0)));
            Assert.IsTrue(grid.CanEnter(terrainStairs, new Vector2Int(1, 0)));
        }

        [Test]
        public void MultipleEntities_CanOccupySeparateCells()
        {
            grid.Occupy(new Vector2Int(0, 0));
            grid.Occupy(new Vector2Int(1, 1));
            grid.Occupy(new Vector2Int(2, 2));

            Assert.IsTrue(grid.IsOccupied(new Vector2Int(0, 0)));
            Assert.IsTrue(grid.IsOccupied(new Vector2Int(1, 1)));
            Assert.IsTrue(grid.IsOccupied(new Vector2Int(2, 2)));
            Assert.IsFalse(grid.IsOccupied(new Vector2Int(3, 3)));
        }

        [Test]
        public void TryGetOccupant_ReturnsFalse_ForUnoccupiedCell()
        {
            bool found = grid.TryGetOccupant(new Vector2Int(5, 5), out GameObject occupant);

            Assert.IsFalse(found);
            Assert.IsNull(occupant);
        }

        [Test]
        public void TryGetOccupant_ReturnsFalse_WhenOccupiedWithoutAnOccupantReference()
        {
            var cell = new Vector2Int(2, 2);
            grid.Occupy(cell);

            bool found = grid.TryGetOccupant(cell, out GameObject occupant);

            Assert.IsFalse(found);
            Assert.IsNull(occupant);
        }

        [Test]
        public void TryGetOccupant_ReturnsTrueAndOccupant_WhenOccupiedWithAReference()
        {
            var cell = new Vector2Int(3, 3);
            var entity = new GameObject("Entity");

            try
            {
                grid.Occupy(cell, entity);

                bool found = grid.TryGetOccupant(cell, out GameObject occupant);

                Assert.IsTrue(found);
                Assert.AreEqual(entity, occupant);
            }
            finally
            {
                Object.DestroyImmediate(entity);
            }
        }

        [Test]
        public void Move_CarriesOccupantReferenceFromSourceToDestination()
        {
            var from = new Vector2Int(1, 1);
            var to = new Vector2Int(4, 4);
            var entity = new GameObject("Entity");

            try
            {
                grid.Occupy(from, entity);
                grid.Move(from, to);

                Assert.IsFalse(grid.TryGetOccupant(from, out _));
                bool found = grid.TryGetOccupant(to, out GameObject occupant);
                Assert.IsTrue(found);
                Assert.AreEqual(entity, occupant);
            }
            finally
            {
                Object.DestroyImmediate(entity);
            }
        }

        [Test]
        public void Vacate_ClearsOccupantReference()
        {
            var cell = new Vector2Int(6, 6);
            var entity = new GameObject("Entity");

            try
            {
                grid.Occupy(cell, entity);
                grid.Vacate(cell);

                Assert.IsFalse(grid.TryGetOccupant(cell, out GameObject occupant));
                Assert.IsNull(occupant);
            }
            finally
            {
                Object.DestroyImmediate(entity);
            }
        }
    }
}
