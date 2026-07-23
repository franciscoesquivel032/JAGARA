using NUnit.Framework;
using Jagara.Runtime.DungeonGen;

namespace Jagara.Tests.EditMode
{
    public class DungeonGeneratorRoomTests
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
        public void GenerateRooms_NoTwoRoomsOverlap()
        {
            var rooms = new DungeonGenerator().GenerateRooms(12345, DefaultParams());

            for (int i = 0; i < rooms.Count; i++)
            {
                for (int j = i + 1; j < rooms.Count; j++)
                {
                    Assert.IsFalse(
                        IndependentOverlapCheck(rooms[i], rooms[j]),
                        $"Room {i} {rooms[i]} overlaps room {j} {rooms[j]}.");
                }
            }
        }

        [Test]
        public void GenerateRooms_AllRoomsWithinGridBounds()
        {
            var parameters = DefaultParams();
            var rooms = new DungeonGenerator().GenerateRooms(999, parameters);

            foreach (var room in rooms)
            {
                Assert.GreaterOrEqual(room.X, 0);
                Assert.GreaterOrEqual(room.Y, 0);
                Assert.LessOrEqual(room.X + room.Width, parameters.GridWidth);
                Assert.LessOrEqual(room.Y + room.Height, parameters.GridHeight);
            }
        }

        [Test]
        public void GenerateRooms_SameSeedProducesIdenticalResults()
        {
            var parameters = DefaultParams();
            var generator = new DungeonGenerator();

            var first = generator.GenerateRooms(42, parameters);
            var second = generator.GenerateRooms(42, parameters);

            Assert.AreEqual(first.Count, second.Count);
            for (int i = 0; i < first.Count; i++)
            {
                Assert.AreEqual(first[i].X, second[i].X);
                Assert.AreEqual(first[i].Y, second[i].Y);
                Assert.AreEqual(first[i].Width, second[i].Width);
                Assert.AreEqual(first[i].Height, second[i].Height);
            }
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(42)]
        [TestCase(1000)]
        [TestCase(7777)]
        [TestCase(99999)]
        public void GenerateRooms_RoomCountWithinConfiguredRange(int seed)
        {
            // Upper bound (<= MaxRoomCount) is a structural guarantee of the algorithm.
            // Lower bound (>= MinRoomCount) is a practical property of these specific,
            // generously-spaced params (see DungeonGenerator XML doc comment), not a
            // guarantee for arbitrary params.
            var parameters = DefaultParams();
            var rooms = new DungeonGenerator().GenerateRooms(seed, parameters);

            Assert.GreaterOrEqual(rooms.Count, parameters.MinRoomCount);
            Assert.LessOrEqual(rooms.Count, parameters.MaxRoomCount);
        }

        private static bool IndependentOverlapCheck(RoomData a, RoomData b)
        {
            return a.X < b.X + b.Width && a.X + a.Width > b.X &&
                   a.Y < b.Y + b.Height && a.Y + a.Height > b.Y;
        }
    }
}
