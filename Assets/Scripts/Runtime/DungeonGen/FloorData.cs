using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Jagara.Runtime.DungeonGen
{
    public class FloorData
    {
        public TileType[,] Grid { get; }
        public List<RoomData> Rooms { get; }
        public Vector2Int? PlayerSpawn { get; }
        public Vector2Int? StairsDownPosition { get; }
        public List<Vector2Int> EnemySpawnPositions { get; }
        public List<Vector2Int> ItemSpawnPositions { get; }

        /// <summary>
        /// The seed this floor was generated from. Not consumed by generation
        /// itself - carried along so rendering (e.g. floor tile variants) can be
        /// deterministic per floor, and as repro metadata.
        /// </summary>
        public int Seed { get; }

        public FloorData(
            TileType[,] grid,
            List<RoomData> rooms,
            Vector2Int? playerSpawn = null,
            Vector2Int? stairsDownPosition = null,
            List<Vector2Int> enemySpawnPositions = null,
            List<Vector2Int> itemSpawnPositions = null,
            int seed = 0)
        {
            Grid = grid;
            Rooms = rooms;
            PlayerSpawn = playerSpawn;
            StairsDownPosition = stairsDownPosition;
            EnemySpawnPositions = enemySpawnPositions ?? new List<Vector2Int>();
            ItemSpawnPositions = itemSpawnPositions ?? new List<Vector2Int>();
            Seed = seed;
        }

        /// <summary>
        /// Renders the tile grid as text for quick visual inspection outside Play Mode:
        /// '#' Wall, '.' Floor, ',' Corridor, '>' StairsDown, '@' PlayerSpawn, 'e' Enemy, 'i' Item.
        /// Spawn/staircase always take rendering priority over enemy/item on the same tile,
        /// even though placement rules should make that overlap impossible in practice.
        /// One line per row (y), left-to-right by x.
        /// </summary>
        public string ToAsciiArt()
        {
            int width = Grid.GetLength(0);
            int height = Grid.GetLength(1);

            var sb = new StringBuilder((width + 1) * height);
            for (int y = 0; y < height; y++)
            {
                if (y > 0)
                {
                    sb.Append('\n');
                }

                for (int x = 0; x < width; x++)
                {
                    sb.Append(ToChar(x, y));
                }
            }

            return sb.ToString();
        }

        private char ToChar(int x, int y)
        {
            if (PlayerSpawn.HasValue && PlayerSpawn.Value.x == x && PlayerSpawn.Value.y == y)
            {
                return '@';
            }

            if (StairsDownPosition.HasValue && StairsDownPosition.Value.x == x && StairsDownPosition.Value.y == y)
            {
                return '>';
            }

            if (ContainsPosition(EnemySpawnPositions, x, y))
            {
                return 'e';
            }

            if (ContainsPosition(ItemSpawnPositions, x, y))
            {
                return 'i';
            }

            return ToChar(Grid[x, y]);
        }

        private static bool ContainsPosition(List<Vector2Int> positions, int x, int y)
        {
            for (int i = 0; i < positions.Count; i++)
            {
                if (positions[i].x == x && positions[i].y == y)
                {
                    return true;
                }
            }

            return false;
        }

        private static char ToChar(TileType tile)
        {
            switch (tile)
            {
                case TileType.Floor:
                    return '.';
                case TileType.Corridor:
                    return ',';
                case TileType.StairsDown:
                    return '>';
                default:
                    return '#';
            }
        }
    }
}
