using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Jagara.Runtime.Data;
using Jagara.Runtime.DungeonGen;

namespace Jagara.Tests.EditMode
{
    public class DungeonGeneratorBatchRobustnessTests
    {
        private const string ParamsAssetPath = "Assets/ScriptableObjects/DungeonGenParams_Nightmare1.asset";

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(42)]
        [TestCase(1000)]
        [TestCase(7777)]
        [TestCase(99999)]
        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(2147483647)]
        [TestCase(20260723)]
        [TestCase(8675309)]
        [TestCase(314159)]
        [TestCase(271828)]
        [TestCase(112358)]
        [TestCase(555555)]
        public void GenerateFloor_PassesAllRobustnessChecks(int seed)
        {
            var parameters = LoadNightmareParams();
            var floor = new DungeonGenerator().GenerateFloor(seed, parameters);

            var failures = new List<string>();
            CheckNoOverlapAndInBounds(seed, parameters, floor.Rooms, failures);
            CheckFullConnectivity(seed, floor, failures);
            CheckSpawnAndStairsValid(seed, floor, failures);
            CheckEnemyAndItemPlacement(seed, parameters, floor, failures);

            Assert.IsEmpty(failures, $"[seed {seed}] {failures.Count} check(s) failed:\n" + string.Join("\n", failures));
        }

        private static DungeonGenerationParams LoadNightmareParams()
        {
            var so = AssetDatabase.LoadAssetAtPath<DungeonGenerationParamsSO>(ParamsAssetPath);
            Assert.IsNotNull(so, $"Could not load DungeonGenerationParamsSO at '{ParamsAssetPath}'.");
            return so.ToParams();
        }

        private static void CheckNoOverlapAndInBounds(int seed, DungeonGenerationParams parameters, List<RoomData> rooms, List<string> failures)
        {
            for (int i = 0; i < rooms.Count; i++)
            {
                var room = rooms[i];

                if (room.X < 0)
                {
                    failures.Add($"[room-bounds] Room {i} {room} has X < 0.");
                }
                if (room.Y < 0)
                {
                    failures.Add($"[room-bounds] Room {i} {room} has Y < 0.");
                }
                if (room.Right > parameters.GridWidth)
                {
                    failures.Add($"[room-bounds] Room {i} {room} extends past GridWidth ({parameters.GridWidth}).");
                }
                if (room.Bottom > parameters.GridHeight)
                {
                    failures.Add($"[room-bounds] Room {i} {room} extends past GridHeight ({parameters.GridHeight}).");
                }

                for (int j = i + 1; j < rooms.Count; j++)
                {
                    if (room.Overlaps(rooms[j]))
                    {
                        failures.Add($"[room-overlap] Room {i} {room} overlaps room {j} {rooms[j]}.");
                    }
                }
            }
        }

        private static void CheckFullConnectivity(int seed, FloorData floor, List<string> failures)
        {
            if (floor.Rooms.Count == 0)
            {
                return;
            }

            var grid = floor.Grid;
            int width = grid.GetLength(0);
            int height = grid.GetLength(1);

            var visited = new bool[width, height];
            var queue = new Queue<Vector2Int>();

            var start = new Vector2Int(floor.Rooms[0].CenterX, floor.Rooms[0].CenterY);
            visited[start.x, start.y] = true;
            queue.Enqueue(start);

            Vector2Int[] directions =
            {
                new Vector2Int(1, 0), new Vector2Int(-1, 0),
                new Vector2Int(0, 1), new Vector2Int(0, -1)
            };

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                foreach (var dir in directions)
                {
                    var next = current + dir;
                    if (next.x < 0 || next.x >= width || next.y < 0 || next.y >= height)
                    {
                        continue;
                    }

                    if (visited[next.x, next.y] || grid[next.x, next.y] == TileType.Wall)
                    {
                        continue;
                    }

                    visited[next.x, next.y] = true;
                    queue.Enqueue(next);
                }
            }

            foreach (var room in floor.Rooms)
            {
                if (!visited[room.CenterX, room.CenterY])
                {
                    failures.Add($"[connectivity] Room {room} was not reachable from room 0 via Floor/Corridor tiles.");
                }
            }
        }

        private static void CheckSpawnAndStairsValid(int seed, FloorData floor, List<string> failures)
        {
            if (!floor.PlayerSpawn.HasValue)
            {
                failures.Add("[spawn-stairs] PlayerSpawn was not set.");
            }
            if (!floor.StairsDownPosition.HasValue)
            {
                failures.Add("[spawn-stairs] StairsDownPosition was not set.");
            }

            var spawn = floor.PlayerSpawn.GetValueOrDefault();
            var stairs = floor.StairsDownPosition.GetValueOrDefault();

            if (floor.Grid[spawn.x, spawn.y] != TileType.Floor)
            {
                failures.Add($"[spawn-stairs] Spawn tile at {spawn} is not Floor.");
            }
            if (floor.Grid[stairs.x, stairs.y] != TileType.StairsDown)
            {
                failures.Add($"[spawn-stairs] Stairs tile at {stairs} is not StairsDown.");
            }
            if (spawn == stairs)
            {
                failures.Add($"[spawn-stairs] Spawn and stairs are on the same tile {spawn}.");
            }

            int spawnRoom = RoomIndexContaining(floor.Rooms, spawn);
            int stairsRoom = RoomIndexContaining(floor.Rooms, stairs);

            if (spawnRoom < 0)
            {
                failures.Add($"[spawn-stairs] Spawn {spawn} is not inside any room.");
            }
            if (stairsRoom < 0)
            {
                failures.Add($"[spawn-stairs] Stairs {stairs} is not inside any room.");
            }
            if (floor.Rooms.Count > 1 && spawnRoom == stairsRoom)
            {
                failures.Add($"[spawn-stairs] Spawn (room {spawnRoom}) and stairs (room {stairsRoom}) are in the same room.");
            }
        }

        private static void CheckEnemyAndItemPlacement(int seed, DungeonGenerationParams parameters, FloorData floor, List<string> failures)
        {
            var spawn = floor.PlayerSpawn.GetValueOrDefault();
            var stairs = floor.StairsDownPosition.GetValueOrDefault();

            if (floor.EnemySpawnPositions.Count < parameters.MinEnemyCount)
            {
                failures.Add($"[enemy-item] Enemy count {floor.EnemySpawnPositions.Count} is below MinEnemyCount {parameters.MinEnemyCount}.");
            }
            if (floor.EnemySpawnPositions.Count > parameters.MaxEnemyCount)
            {
                failures.Add($"[enemy-item] Enemy count {floor.EnemySpawnPositions.Count} exceeds MaxEnemyCount {parameters.MaxEnemyCount}.");
            }
            if (floor.ItemSpawnPositions.Count < parameters.MinItemCount)
            {
                failures.Add($"[enemy-item] Item count {floor.ItemSpawnPositions.Count} is below MinItemCount {parameters.MinItemCount}.");
            }
            if (floor.ItemSpawnPositions.Count > parameters.MaxItemCount)
            {
                failures.Add($"[enemy-item] Item count {floor.ItemSpawnPositions.Count} exceeds MaxItemCount {parameters.MaxItemCount}.");
            }

            foreach (var pos in floor.EnemySpawnPositions)
            {
                if (pos == spawn)
                {
                    failures.Add($"[enemy-item] Enemy at {pos} is on the spawn tile.");
                }
                if (pos == stairs)
                {
                    failures.Add($"[enemy-item] Enemy at {pos} is on the stairs tile.");
                }
                if (floor.Grid[pos.x, pos.y] == TileType.Corridor)
                {
                    failures.Add($"[enemy-item] Enemy at {pos} sits on a Corridor tile.");
                }
            }

            foreach (var pos in floor.ItemSpawnPositions)
            {
                if (pos == spawn)
                {
                    failures.Add($"[enemy-item] Item at {pos} is on the spawn tile.");
                }
                if (pos == stairs)
                {
                    failures.Add($"[enemy-item] Item at {pos} is on the stairs tile.");
                }
                if (floor.Grid[pos.x, pos.y] == TileType.Corridor)
                {
                    failures.Add($"[enemy-item] Item at {pos} sits on a Corridor tile.");
                }
            }

            var enemyTiles = new HashSet<Vector2Int>(floor.EnemySpawnPositions);
            foreach (var itemPos in floor.ItemSpawnPositions)
            {
                if (enemyTiles.Contains(itemPos))
                {
                    failures.Add($"[enemy-item] Item at {itemPos} shares a tile with an enemy.");
                }
            }
        }

        private static int RoomIndexContaining(List<RoomData> rooms, Vector2Int pos)
        {
            for (int i = 0; i < rooms.Count; i++)
            {
                var room = rooms[i];
                if (pos.x >= room.X && pos.x < room.Right && pos.y >= room.Y && pos.y < room.Bottom)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
