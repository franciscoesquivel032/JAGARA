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

        public FloorData(TileType[,] grid, List<RoomData> rooms, Vector2Int? playerSpawn = null, Vector2Int? stairsDownPosition = null)
        {
            Grid = grid;
            Rooms = rooms;
            PlayerSpawn = playerSpawn;
            StairsDownPosition = stairsDownPosition;
        }

        /// <summary>
        /// Renders the tile grid as text for quick visual inspection outside Play Mode:
        /// '#' Wall, '.' Floor, ',' Corridor, '>' StairsDown, '@' PlayerSpawn.
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

            return ToChar(Grid[x, y]);
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
