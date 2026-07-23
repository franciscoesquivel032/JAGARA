using NUnit.Framework;
using Jagara.Runtime.DungeonGen;

namespace Jagara.Tests.EditMode
{
    public class FloorDataAsciiArtTests
    {
        [Test]
        public void ToAsciiArt_LineCountAndLineLengthMatchGridDimensions()
        {
            var grid = new TileType[5, 3];
            var floor = new FloorData(grid, new System.Collections.Generic.List<RoomData>());

            var lines = floor.ToAsciiArt().Split('\n');

            Assert.AreEqual(3, lines.Length);
            foreach (var line in lines)
            {
                Assert.AreEqual(5, line.Length);
            }
        }

        [Test]
        public void ToAsciiArt_RendersEachTileTypeWithExpectedCharacter()
        {
            var grid = new TileType[3, 2];
            grid[0, 0] = TileType.Wall;
            grid[1, 0] = TileType.Floor;
            grid[2, 0] = TileType.Corridor;
            grid[0, 1] = TileType.Corridor;
            grid[1, 1] = TileType.Wall;
            grid[2, 1] = TileType.Floor;

            var floor = new FloorData(grid, new System.Collections.Generic.List<RoomData>());

            var lines = floor.ToAsciiArt().Split('\n');

            Assert.AreEqual('#', lines[0][0]);
            Assert.AreEqual('.', lines[0][1]);
            Assert.AreEqual(',', lines[0][2]);
            Assert.AreEqual(',', lines[1][0]);
            Assert.AreEqual('#', lines[1][1]);
            Assert.AreEqual('.', lines[1][2]);
        }
    }
}
