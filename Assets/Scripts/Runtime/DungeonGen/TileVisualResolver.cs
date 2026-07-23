namespace Jagara.Runtime.DungeonGen
{
    /// <summary>
    /// Pure visual-selection logic for floor instantiation: maps a wall cell's
    /// neighborhood to a WallShape, and a floor/corridor cell to a weighted
    /// variant index. Deterministic and free of UnityEngine.Tilemaps so it can
    /// run in Edit Mode tests.
    /// </summary>
    public static class TileVisualResolver
    {
        private const int North = 1;
        private const int East = 2;
        private const int South = 4;
        private const int West = 8;

        /// <summary>
        /// Resolves the visual shape of the wall cell at (x, y). A neighbor is
        /// "open" when it is walkable ground (Floor/Corridor/StairsDown);
        /// out-of-bounds counts as wall. Cardinal neighbors decide edges and
        /// outer corners; diagonals only matter when all cardinals are closed,
        /// where a single open diagonal produces an inner corner.
        /// Neighborhoods without dedicated art (1-wide slivers, corridor caps,
        /// isolated pillars) fall back to the closest of the 13 shapes - see the
        /// explicit cases below.
        /// </summary>
        public static WallShape ResolveWallShape(TileType[,] grid, int x, int y)
        {
            int mask = 0;
            if (IsOpen(grid, x, y + 1)) mask |= North;
            if (IsOpen(grid, x + 1, y)) mask |= East;
            if (IsOpen(grid, x, y - 1)) mask |= South;
            if (IsOpen(grid, x - 1, y)) mask |= West;

            switch (mask)
            {
                case 0:
                    break; // all cardinals closed - diagonals decide below
                case North:
                    return WallShape.EdgeN;
                case East:
                    return WallShape.EdgeE;
                case South:
                    return WallShape.EdgeS;
                case West:
                    return WallShape.EdgeW;
                case North | East:
                    return WallShape.OuterNE;
                case East | South:
                    return WallShape.OuterSE;
                case South | West:
                    return WallShape.OuterSW;
                case West | North:
                    return WallShape.OuterNW;
                case North | South: // 1-wide horizontal sliver
                    return WallShape.EdgeN;
                case East | West: // 1-wide vertical sliver
                    return WallShape.EdgeE;
                case North | East | South: // caps: wall stub open on three sides
                    return WallShape.OuterNE;
                case East | South | West:
                    return WallShape.OuterSE;
                case South | West | North:
                    return WallShape.OuterSW;
                case West | North | East:
                    return WallShape.OuterNE;
                default: // all four open: isolated pillar
                    return WallShape.Fill;
            }

            // Inner corners, priority NE > NW > SE > SW when several are open.
            if (IsOpen(grid, x + 1, y + 1))
            {
                return WallShape.InnerNE;
            }

            if (IsOpen(grid, x - 1, y + 1))
            {
                return WallShape.InnerNW;
            }

            if (IsOpen(grid, x + 1, y - 1))
            {
                return WallShape.InnerSE;
            }

            if (IsOpen(grid, x - 1, y - 1))
            {
                return WallShape.InnerSW;
            }

            return WallShape.Fill;
        }

        /// <summary>
        /// Deterministically picks a floor variant (0 = primary, 1 = secondary,
        /// 2 = tertiary) for the cell at (x, y) by hashing (floorSeed, x, y) and
        /// thresholding against the integer weights. A zero weight makes that
        /// variant unreachable; a non-positive total degenerates to primary.
        /// </summary>
        public static int ResolveFloorVariant(int floorSeed, int x, int y,
            int primaryWeight, int secondaryWeight, int tertiaryWeight)
        {
            int total = primaryWeight + secondaryWeight + tertiaryWeight;
            if (total <= 0)
            {
                return 0;
            }

            unchecked
            {
                uint h = (uint)floorSeed;
                h ^= (uint)x * 0x9E3779B1u;
                h = (h ^ (h >> 16)) * 0x85EBCA6Bu;
                h ^= (uint)y * 0xC2B2AE35u;
                h = (h ^ (h >> 13)) * 0x27D4EB2Fu;
                h ^= h >> 16;

                int roll = (int)(h % (uint)total);
                if (roll < primaryWeight)
                {
                    return 0;
                }

                return roll < primaryWeight + secondaryWeight ? 1 : 2;
            }
        }

        private static bool IsOpen(TileType[,] grid, int x, int y)
        {
            if (x < 0 || y < 0 || x >= grid.GetLength(0) || y >= grid.GetLength(1))
            {
                return false;
            }

            return grid[x, y] != TileType.Wall;
        }
    }
}
