using System.Collections.Generic;
using NUnit.Framework;
using Jagara.Runtime.DungeonGen;

namespace Jagara.Tests.EditMode
{
    public class DungeonGeneratorFloorTests
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

        [Test]
        public void GenerateFloor_SingleRoom_ProducesNoCorridorsWithoutError()
        {
            var parameters = new DungeonGenerationParams
            {
                GridWidth = 20,
                GridHeight = 20,
                MinRoomCount = 1,
                MaxRoomCount = 1,
                MinRoomWidth = 4,
                MaxRoomWidth = 8,
                MinRoomHeight = 4,
                MaxRoomHeight = 8,
            };

            var floor = new DungeonGenerator().GenerateFloor(555, parameters);

            Assert.AreEqual(1, floor.Rooms.Count);

            for (int x = 0; x < parameters.GridWidth; x++)
            {
                for (int y = 0; y < parameters.GridHeight; y++)
                {
                    Assert.AreNotEqual(TileType.Corridor, floor.Grid[x, y]);
                }
            }
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(42)]
        [TestCase(1000)]
        [TestCase(7777)]
        [TestCase(99999)]
        public void GenerateFloor_AllRoomsAreMutuallyReachable(int seed)
        {
            var parameters = DefaultParams();
            var floor = new DungeonGenerator().GenerateFloor(seed, parameters);

            var visited = new bool[parameters.GridWidth, parameters.GridHeight];
            var startRoom = floor.Rooms[0];
            var queue = new Queue<(int x, int y)>();
            queue.Enqueue((startRoom.CenterX, startRoom.CenterY));
            visited[startRoom.CenterX, startRoom.CenterY] = true;

            int[] dx = { 1, -1, 0, 0 };
            int[] dy = { 0, 0, 1, -1 };

            while (queue.Count > 0)
            {
                var (x, y) = queue.Dequeue();
                for (int i = 0; i < 4; i++)
                {
                    int nx = x + dx[i];
                    int ny = y + dy[i];
                    if (nx < 0 || nx >= parameters.GridWidth || ny < 0 || ny >= parameters.GridHeight)
                        continue;
                    if (visited[nx, ny])
                        continue;
                    if (floor.Grid[nx, ny] == TileType.Wall)
                        continue;

                    visited[nx, ny] = true;
                    queue.Enqueue((nx, ny));
                }
            }

            foreach (var room in floor.Rooms)
            {
                Assert.IsTrue(
                    visited[room.CenterX, room.CenterY],
                    $"Room {room} was not reached from room {startRoom} via floor/corridor tiles.");
            }
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(42)]
        [TestCase(1000)]
        [TestCase(7777)]
        [TestCase(99999)]
        public void GenerateFloor_CorridorTilesStayWithinGridBounds(int seed)
        {
            var parameters = DefaultParams();

            var floor = new DungeonGenerator().GenerateFloor(seed, parameters);

            Assert.AreEqual(parameters.GridWidth, floor.Grid.GetLength(0));
            Assert.AreEqual(parameters.GridHeight, floor.Grid.GetLength(1));

            bool anyCorridor = false;
            for (int x = 0; x < parameters.GridWidth; x++)
            {
                for (int y = 0; y < parameters.GridHeight; y++)
                {
                    if (floor.Grid[x, y] == TileType.Corridor)
                    {
                        anyCorridor = true;
                    }
                }
            }

            Assert.IsTrue(anyCorridor, "Expected at least one corridor tile connecting multiple rooms.");
        }

        [Test]
        public void GenerateFloor_SameSeedProducesIdenticalGrid()
        {
            var parameters = DefaultParams();
            var generator = new DungeonGenerator();

            var first = generator.GenerateFloor(2024, parameters);
            var second = generator.GenerateFloor(2024, parameters);

            for (int x = 0; x < parameters.GridWidth; x++)
            {
                for (int y = 0; y < parameters.GridHeight; y++)
                {
                    Assert.AreEqual(
                        first.Grid[x, y], second.Grid[x, y],
                        $"Grid mismatch at ({x},{y}).");
                }
            }
        }
    }
}
