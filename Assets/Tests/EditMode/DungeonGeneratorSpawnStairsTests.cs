using NUnit.Framework;
using UnityEngine;
using Jagara.Runtime.DungeonGen;

namespace Jagara.Tests.EditMode
{
    public class DungeonGeneratorSpawnStairsTests
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
        };

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(42)]
        [TestCase(1000)]
        [TestCase(7777)]
        [TestCase(99999)]
        public void GenerateFloor_PlayerSpawnIsAlwaysOnFloorTile(int seed)
        {
            var parameters = DefaultParams();
            var floor = new DungeonGenerator().GenerateFloor(seed, parameters);

            Assert.IsTrue(floor.PlayerSpawn.HasValue);
            var spawn = floor.PlayerSpawn.Value;

            Assert.AreEqual(TileType.Floor, floor.Grid[spawn.x, spawn.y]);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(42)]
        [TestCase(1000)]
        [TestCase(7777)]
        [TestCase(99999)]
        public void GenerateFloor_StairsDownOverwritesAFloorTileAndIsNotOnSpawn(int seed)
        {
            var parameters = DefaultParams();
            var floor = new DungeonGenerator().GenerateFloor(seed, parameters);

            Assert.IsTrue(floor.StairsDownPosition.HasValue);
            var stairs = floor.StairsDownPosition.Value;
            var spawn = floor.PlayerSpawn.Value;

            Assert.AreEqual(TileType.StairsDown, floor.Grid[stairs.x, stairs.y]);
            Assert.AreNotEqual(spawn, stairs);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(42)]
        [TestCase(1000)]
        [TestCase(7777)]
        [TestCase(99999)]
        public void GenerateFloor_StairsDownIsNeverInTheSameRoomAsSpawn(int seed)
        {
            var parameters = DefaultParams();
            var floor = new DungeonGenerator().GenerateFloor(seed, parameters);

            var spawn = floor.PlayerSpawn.Value;
            var stairs = floor.StairsDownPosition.Value;

            RoomData? spawnRoom = null;
            RoomData? stairsRoom = null;
            foreach (var room in floor.Rooms)
            {
                if (spawn.x >= room.X && spawn.x < room.Right && spawn.y >= room.Y && spawn.y < room.Bottom)
                {
                    spawnRoom = room;
                }
                if (stairs.x >= room.X && stairs.x < room.Right && stairs.y >= room.Y && stairs.y < room.Bottom)
                {
                    stairsRoom = room;
                }
            }

            Assert.IsTrue(spawnRoom.HasValue, "Spawn was not found inside any room.");
            Assert.IsTrue(stairsRoom.HasValue, "Stairs was not found inside any room.");
            Assert.AreNotEqual(spawnRoom.Value, stairsRoom.Value);
        }

        [Test]
        public void GenerateFloor_SameSeedProducesIdenticalSpawnAndStairs()
        {
            var parameters = DefaultParams();
            var generator = new DungeonGenerator();

            var first = generator.GenerateFloor(4242, parameters);
            var second = generator.GenerateFloor(4242, parameters);

            Assert.AreEqual(first.PlayerSpawn, second.PlayerSpawn);
            Assert.AreEqual(first.StairsDownPosition, second.StairsDownPosition);
        }
    }
}
