using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Jagara.Runtime.DungeonGen;

namespace Jagara.Tests.EditMode
{
    public class DungeonGeneratorEnemyItemTests
    {
        private static DungeonGenerationParams DefaultParams() => new DungeonGenerationParams
        {
            GridWidth = 50,
            GridHeight = 50,
            MinRoomCount = 5,
            MaxRoomCount = 10,
            MinRoomWidth = 4,
            MaxRoomWidth = 8,
            MinRoomHeight = 4,
            MaxRoomHeight = 8,
            MinEnemyCount = 5,
            MaxEnemyCount = 8,
            MinItemCount = 4,
            MaxItemCount = 6,
        };

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(42)]
        [TestCase(1000)]
        [TestCase(7777)]
        [TestCase(99999)]
        public void GenerateFloor_EnemyCountIsWithinConfiguredRange(int seed)
        {
            var parameters = DefaultParams();
            var floor = new DungeonGenerator().GenerateFloor(seed, parameters);

            Assert.GreaterOrEqual(floor.EnemySpawnPositions.Count, parameters.MinEnemyCount);
            Assert.LessOrEqual(floor.EnemySpawnPositions.Count, parameters.MaxEnemyCount);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(42)]
        [TestCase(1000)]
        [TestCase(7777)]
        [TestCase(99999)]
        public void GenerateFloor_ItemCountIsWithinConfiguredRange(int seed)
        {
            var parameters = DefaultParams();
            var floor = new DungeonGenerator().GenerateFloor(seed, parameters);

            Assert.GreaterOrEqual(floor.ItemSpawnPositions.Count, parameters.MinItemCount);
            Assert.LessOrEqual(floor.ItemSpawnPositions.Count, parameters.MaxItemCount);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(42)]
        [TestCase(1000)]
        [TestCase(7777)]
        [TestCase(99999)]
        public void GenerateFloor_NoEnemyOrItemSpawnsOnPlayerSpawnTile(int seed)
        {
            var parameters = DefaultParams();
            var floor = new DungeonGenerator().GenerateFloor(seed, parameters);
            var spawn = floor.PlayerSpawn.Value;

            CollectionAssert.DoesNotContain(floor.EnemySpawnPositions, spawn);
            CollectionAssert.DoesNotContain(floor.ItemSpawnPositions, spawn);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(42)]
        [TestCase(1000)]
        [TestCase(7777)]
        [TestCase(99999)]
        public void GenerateFloor_NoEnemyOrItemSpawnsOnStairsTile(int seed)
        {
            var parameters = DefaultParams();
            var floor = new DungeonGenerator().GenerateFloor(seed, parameters);
            var stairs = floor.StairsDownPosition.Value;

            CollectionAssert.DoesNotContain(floor.EnemySpawnPositions, stairs);
            CollectionAssert.DoesNotContain(floor.ItemSpawnPositions, stairs);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(42)]
        [TestCase(1000)]
        [TestCase(7777)]
        [TestCase(99999)]
        public void GenerateFloor_NoEnemyOrItemSpawnsOnACorridorTile(int seed)
        {
            var parameters = DefaultParams();
            var floor = new DungeonGenerator().GenerateFloor(seed, parameters);

            foreach (var pos in floor.EnemySpawnPositions)
            {
                Assert.AreNotEqual(TileType.Corridor, floor.Grid[pos.x, pos.y]);
            }

            foreach (var pos in floor.ItemSpawnPositions)
            {
                Assert.AreNotEqual(TileType.Corridor, floor.Grid[pos.x, pos.y]);
            }
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(42)]
        [TestCase(1000)]
        [TestCase(7777)]
        [TestCase(99999)]
        public void GenerateFloor_NoEnemyAndItemShareTheSameTile(int seed)
        {
            var parameters = DefaultParams();
            var floor = new DungeonGenerator().GenerateFloor(seed, parameters);

            var enemyTiles = new HashSet<Vector2Int>(floor.EnemySpawnPositions);
            foreach (var itemPos in floor.ItemSpawnPositions)
            {
                Assert.IsFalse(enemyTiles.Contains(itemPos), $"Item at {itemPos} shares a tile with an enemy.");
            }
        }

        [Test]
        public void GenerateFloor_SameSeedProducesIdenticalEnemyAndItemPositions()
        {
            var parameters = DefaultParams();
            var generator = new DungeonGenerator();

            var first = generator.GenerateFloor(8675309, parameters);
            var second = generator.GenerateFloor(8675309, parameters);

            CollectionAssert.AreEqual(first.EnemySpawnPositions, second.EnemySpawnPositions);
            CollectionAssert.AreEqual(first.ItemSpawnPositions, second.ItemSpawnPositions);
        }
    }
}
