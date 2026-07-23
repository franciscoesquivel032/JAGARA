using NUnit.Framework;
using Jagara.Runtime.DungeonGen;

namespace Jagara.Tests.EditMode
{
    public class TileVisualResolverWallShapeTests
    {
        // Rows are written top-to-bottom for visual readability, so row 0 is the
        // NORTH edge of the grid; the helper flips into grid[x, y] with +y = north.
        // '#' Wall, '.' Floor, ',' Corridor, '>' StairsDown.
        private static TileType[,] GridFromRows(params string[] rows)
        {
            int height = rows.Length;
            int width = rows[0].Length;
            var grid = new TileType[width, height];
            for (int row = 0; row < height; row++)
            {
                for (int x = 0; x < width; x++)
                {
                    grid[x, height - 1 - row] = CharToTile(rows[row][x]);
                }
            }

            return grid;
        }

        private static TileType CharToTile(char c)
        {
            switch (c)
            {
                case '.':
                    return TileType.Floor;
                case ',':
                    return TileType.Corridor;
                case '>':
                    return TileType.StairsDown;
                default:
                    return TileType.Wall;
            }
        }

        private static WallShape ResolveCenter(params string[] rows)
        {
            return TileVisualResolver.ResolveWallShape(GridFromRows(rows), 1, 1);
        }

        // --- Canonical shapes ---

        [Test]
        public void AllWallNeighbors_ResolvesFill()
        {
            Assert.AreEqual(WallShape.Fill, ResolveCenter(
                "###",
                "###",
                "###"));
        }

        [Test]
        public void OpenNorthOnly_ResolvesEdgeN()
        {
            Assert.AreEqual(WallShape.EdgeN, ResolveCenter(
                "#.#",
                "###",
                "###"));
        }

        [Test]
        public void OpenSouthOnly_ResolvesEdgeS()
        {
            Assert.AreEqual(WallShape.EdgeS, ResolveCenter(
                "###",
                "###",
                "#.#"));
        }

        [Test]
        public void OpenEastOnly_ResolvesEdgeE()
        {
            Assert.AreEqual(WallShape.EdgeE, ResolveCenter(
                "###",
                "##.",
                "###"));
        }

        [Test]
        public void OpenWestOnly_ResolvesEdgeW()
        {
            Assert.AreEqual(WallShape.EdgeW, ResolveCenter(
                "###",
                ".##",
                "###"));
        }

        [Test]
        public void OpenNorthAndEast_ResolvesOuterNE()
        {
            Assert.AreEqual(WallShape.OuterNE, ResolveCenter(
                "#.#",
                "##.",
                "###"));
        }

        [Test]
        public void OpenNorthAndWest_ResolvesOuterNW()
        {
            Assert.AreEqual(WallShape.OuterNW, ResolveCenter(
                "#.#",
                ".##",
                "###"));
        }

        [Test]
        public void OpenSouthAndEast_ResolvesOuterSE()
        {
            Assert.AreEqual(WallShape.OuterSE, ResolveCenter(
                "###",
                "##.",
                "#.#"));
        }

        [Test]
        public void OpenSouthAndWest_ResolvesOuterSW()
        {
            Assert.AreEqual(WallShape.OuterSW, ResolveCenter(
                "###",
                ".##",
                "#.#"));
        }

        [Test]
        public void OpenNorthEastDiagonalOnly_ResolvesInnerNE()
        {
            Assert.AreEqual(WallShape.InnerNE, ResolveCenter(
                "##.",
                "###",
                "###"));
        }

        [Test]
        public void OpenNorthWestDiagonalOnly_ResolvesInnerNW()
        {
            Assert.AreEqual(WallShape.InnerNW, ResolveCenter(
                ".##",
                "###",
                "###"));
        }

        [Test]
        public void OpenSouthEastDiagonalOnly_ResolvesInnerSE()
        {
            Assert.AreEqual(WallShape.InnerSE, ResolveCenter(
                "###",
                "###",
                "##."));
        }

        [Test]
        public void OpenSouthWestDiagonalOnly_ResolvesInnerSW()
        {
            Assert.AreEqual(WallShape.InnerSW, ResolveCenter(
                "###",
                "###",
                ".##"));
        }

        // --- Fallbacks for shapes without dedicated art ---

        [Test]
        public void NorthSouthSliver_FallsBackToEdgeN()
        {
            Assert.AreEqual(WallShape.EdgeN, ResolveCenter(
                "#.#",
                "###",
                "#.#"));
        }

        [Test]
        public void EastWestSliver_FallsBackToEdgeE()
        {
            Assert.AreEqual(WallShape.EdgeE, ResolveCenter(
                "###",
                ".#.",
                "###"));
        }

        [Test]
        public void CapOpenNorthEastSouth_FallsBackToOuterNE()
        {
            Assert.AreEqual(WallShape.OuterNE, ResolveCenter(
                "#.#",
                "##.",
                "#.#"));
        }

        [Test]
        public void CapOpenEastSouthWest_FallsBackToOuterSE()
        {
            Assert.AreEqual(WallShape.OuterSE, ResolveCenter(
                "###",
                ".#.",
                "#.#"));
        }

        [Test]
        public void CapOpenSouthWestNorth_FallsBackToOuterSW()
        {
            Assert.AreEqual(WallShape.OuterSW, ResolveCenter(
                "#.#",
                ".##",
                "#.#"));
        }

        [Test]
        public void CapOpenWestNorthEast_FallsBackToOuterNE()
        {
            Assert.AreEqual(WallShape.OuterNE, ResolveCenter(
                "#.#",
                ".#.",
                "###"));
        }

        [Test]
        public void PillarAllCardinalsOpen_FallsBackToFill()
        {
            Assert.AreEqual(WallShape.Fill, ResolveCenter(
                "#.#",
                ".#.",
                "#.#"));
        }

        [Test]
        public void MultipleOpenDiagonals_PrefersInnerNE()
        {
            Assert.AreEqual(WallShape.InnerNE, ResolveCenter(
                "##.",
                "###",
                ".##"));
        }

        // --- Border handling: out-of-bounds counts as wall ---

        [Test]
        public void CornerOfAllWallGrid_ResolvesFill()
        {
            var grid = GridFromRows(
                "##",
                "##");

            Assert.AreEqual(WallShape.Fill, TileVisualResolver.ResolveWallShape(grid, 0, 0));
        }

        [Test]
        public void BorderWallWithFloorInward_ResolvesEdgeTowardFloor()
        {
            var grid = GridFromRows(
                "###",
                "#.#",
                "###");

            // Probe the southmost middle wall (x=1, y=0): floor is to its north.
            Assert.AreEqual(WallShape.EdgeN, TileVisualResolver.ResolveWallShape(grid, 1, 0));
        }

        // --- Openness semantics ---

        [Test]
        public void CorridorNeighbor_CountsAsOpen()
        {
            Assert.AreEqual(WallShape.EdgeN, ResolveCenter(
                "#,#",
                "###",
                "###"));
        }

        [Test]
        public void StairsDownNeighbor_CountsAsOpen()
        {
            Assert.AreEqual(WallShape.EdgeN, ResolveCenter(
                "#>#",
                "###",
                "###"));
        }
    }
}
