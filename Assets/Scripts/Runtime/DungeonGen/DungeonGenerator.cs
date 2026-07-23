using System;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

namespace Jagara.Runtime.DungeonGen
{
    public class DungeonGenerator
    {
        private const int MaxAttemptsPerRoom = 100;

        /// <summary>
        /// Generates a set of non-overlapping rectangular rooms within the grid bounds
        /// described by <paramref name="parameters"/>. Deterministic: the same seed and
        /// params always produce the same room list.
        ///
        /// The returned count is structurally guaranteed to never exceed MaxRoomCount.
        /// It is NOT mathematically guaranteed to reach MinRoomCount for arbitrary params —
        /// this is naive random-placement-with-retry, so pathological params (e.g. room
        /// sizes close to the grid size) can legitimately terminate early with fewer rooms
        /// once free space fragments. Callers should leave generous headroom between room
        /// sizes/counts and grid size if the lower bound matters to them.
        /// </summary>
        public List<RoomData> GenerateRooms(int seed, DungeonGenerationParams parameters)
        {
            ValidateParams(parameters);
            return GenerateRoomsCore(new Random(seed), parameters);
        }

        /// <summary>
        /// Generates a fully connected floor: rooms painted as Floor, then joined with
        /// Corridor tiles so every room is reachable from every other room. Deterministic:
        /// the same seed and params always produce an identical grid.
        /// </summary>
        public FloorData GenerateFloor(int seed, DungeonGenerationParams parameters)
        {
            ValidateParams(parameters);

            var rng = new Random(seed);
            var rooms = GenerateRoomsCore(rng, parameters);

            var grid = new TileType[parameters.GridWidth, parameters.GridHeight];
            PaintRooms(grid, rooms);

            if (rooms.Count > 1)
            {
                var mstEdges = BuildMinimumSpanningTree(rooms);
                CarveCorridors(grid, rooms, mstEdges, rng);
            }

            PlaceSpawnAndStairs(grid, rooms, rng, out var playerSpawn, out var stairsDown);

            return new FloorData(grid, rooms, playerSpawn, stairsDown);
        }

        // Spawn sits at a room's center, which PaintRooms guarantees is Floor. The
        // staircase sits at a different room's center whenever more than one room
        // exists, so the two never share a room; single-room floors fall back to a
        // corner of the same room instead, since no other room is available.
        private static void PlaceSpawnAndStairs(TileType[,] grid, List<RoomData> rooms, Random rng, out Vector2Int playerSpawn, out Vector2Int stairsDown)
        {
            int spawnRoomIndex = rng.Next(rooms.Count);
            var spawnRoom = rooms[spawnRoomIndex];
            playerSpawn = new Vector2Int(spawnRoom.CenterX, spawnRoom.CenterY);

            if (rooms.Count == 1)
            {
                var corner = new Vector2Int(spawnRoom.Right - 1, spawnRoom.Bottom - 1);
                stairsDown = corner == playerSpawn ? new Vector2Int(spawnRoom.X, spawnRoom.Y) : corner;
            }
            else
            {
                int stairsRoomIndex = rng.Next(rooms.Count - 1);
                if (stairsRoomIndex >= spawnRoomIndex)
                {
                    stairsRoomIndex++;
                }

                var stairsRoom = rooms[stairsRoomIndex];
                stairsDown = new Vector2Int(stairsRoom.CenterX, stairsRoom.CenterY);
            }

            grid[stairsDown.x, stairsDown.y] = TileType.StairsDown;
        }

        // Prim's algorithm over room centers using squared Euclidean distance (avoids
        // Math.Sqrt so the comparison stays in exact integer arithmetic; squared distance
        // is monotonic with true distance, so the resulting tree is identical either way).
        // Plain index-driven arrays only (no Dictionary/HashSet) so edge order is
        // reproducible for a given seed.
        private static List<(int a, int b)> BuildMinimumSpanningTree(List<RoomData> rooms)
        {
            int n = rooms.Count;
            var inMst = new bool[n];
            var minEdgeCost = new long[n];
            var parent = new int[n];
            var edges = new List<(int a, int b)>(n - 1);

            for (int i = 0; i < n; i++)
            {
                minEdgeCost[i] = long.MaxValue;
                parent[i] = -1;
            }
            minEdgeCost[0] = 0;

            for (int iteration = 0; iteration < n; iteration++)
            {
                int u = -1;
                long best = long.MaxValue;
                for (int i = 0; i < n; i++)
                {
                    if (!inMst[i] && minEdgeCost[i] < best)
                    {
                        best = minEdgeCost[i];
                        u = i;
                    }
                }

                inMst[u] = true;
                if (parent[u] != -1)
                {
                    edges.Add((parent[u], u));
                }

                for (int v = 0; v < n; v++)
                {
                    if (inMst[v])
                    {
                        continue;
                    }

                    long cost = SquaredDistance(rooms[u], rooms[v]);
                    if (cost < minEdgeCost[v])
                    {
                        minEdgeCost[v] = cost;
                        parent[v] = u;
                    }
                }
            }

            return edges;
        }

        private static long SquaredDistance(RoomData a, RoomData b)
        {
            long dx = a.CenterX - b.CenterX;
            long dy = a.CenterY - b.CenterY;
            return dx * dx + dy * dy;
        }

        private static void CarveCorridors(TileType[,] grid, List<RoomData> rooms, List<(int a, int b)> edges, Random rng)
        {
            foreach (var (a, b) in edges)
            {
                int ax = rooms[a].CenterX;
                int ay = rooms[a].CenterY;
                int bx = rooms[b].CenterX;
                int by = rooms[b].CenterY;

                bool horizontalFirst = rng.Next(2) == 0;
                if (horizontalFirst)
                {
                    CarveHorizontal(grid, ax, bx, ay);
                    CarveVertical(grid, ay, by, bx);
                }
                else
                {
                    CarveVertical(grid, ay, by, ax);
                    CarveHorizontal(grid, ax, bx, by);
                }
            }
        }

        private static void CarveHorizontal(TileType[,] grid, int x1, int x2, int y)
        {
            int lo = Math.Min(x1, x2);
            int hi = Math.Max(x1, x2);
            for (int x = lo; x <= hi; x++)
            {
                SetCorridor(grid, x, y);
            }
        }

        private static void CarveVertical(TileType[,] grid, int y1, int y2, int x)
        {
            int lo = Math.Min(y1, y2);
            int hi = Math.Max(y1, y2);
            for (int y = lo; y <= hi; y++)
            {
                SetCorridor(grid, x, y);
            }
        }

        private static void SetCorridor(TileType[,] grid, int x, int y)
        {
            if ((uint)x >= (uint)grid.GetLength(0) || (uint)y >= (uint)grid.GetLength(1))
            {
                return;
            }

            if (grid[x, y] == TileType.Wall)
            {
                grid[x, y] = TileType.Corridor;
            }
        }

        private static List<RoomData> GenerateRoomsCore(Random rng, DungeonGenerationParams parameters)
        {
            var rooms = new List<RoomData>();

            int targetCount = rng.Next(parameters.MinRoomCount, parameters.MaxRoomCount + 1);
            int maxTotalAttempts = targetCount * MaxAttemptsPerRoom;
            int attempts = 0;

            while (rooms.Count < targetCount && attempts < maxTotalAttempts)
            {
                attempts++;

                int width = rng.Next(parameters.MinRoomWidth, parameters.MaxRoomWidth + 1);
                int height = rng.Next(parameters.MinRoomHeight, parameters.MaxRoomHeight + 1);
                int x = rng.Next(0, parameters.GridWidth - width + 1);
                int y = rng.Next(0, parameters.GridHeight - height + 1);

                var candidate = new RoomData(x, y, width, height);

                if (!OverlapsAny(candidate, rooms))
                {
                    rooms.Add(candidate);
                }
            }

            return rooms;
        }

        private static void PaintRooms(TileType[,] grid, List<RoomData> rooms)
        {
            foreach (var room in rooms)
            {
                for (int x = room.X; x < room.Right; x++)
                {
                    for (int y = room.Y; y < room.Bottom; y++)
                    {
                        grid[x, y] = TileType.Floor;
                    }
                }
            }
        }

        private static bool OverlapsAny(RoomData candidate, List<RoomData> rooms)
        {
            for (int i = 0; i < rooms.Count; i++)
            {
                if (candidate.Overlaps(rooms[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static void ValidateParams(DungeonGenerationParams p)
        {
            if (p.MinRoomCount < 1 || p.MaxRoomCount < p.MinRoomCount)
                throw new ArgumentException("Invalid room count range.", nameof(p));

            if (p.MinRoomWidth < 1 || p.MaxRoomWidth < p.MinRoomWidth)
                throw new ArgumentException("Invalid room width range.", nameof(p));

            if (p.MinRoomHeight < 1 || p.MaxRoomHeight < p.MinRoomHeight)
                throw new ArgumentException("Invalid room height range.", nameof(p));

            if (p.MaxRoomWidth > p.GridWidth || p.MaxRoomHeight > p.GridHeight)
                throw new ArgumentException("Room size exceeds grid bounds.", nameof(p));
        }
    }
}
