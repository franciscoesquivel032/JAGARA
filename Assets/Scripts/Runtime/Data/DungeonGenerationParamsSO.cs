using Jagara.Runtime.DungeonGen;
using UnityEngine;

namespace Jagara.Runtime.Data
{
    [CreateAssetMenu(fileName = "New Dungeon Generation Params", menuName = "Jagara/Dungeon Generation Params")]
    public class DungeonGenerationParamsSO : ScriptableObject
    {
        [Tooltip("Width of the floor grid, in tiles.")]
        [SerializeField] private int gridWidth = 50;

        [Tooltip("Height of the floor grid, in tiles.")]
        [SerializeField] private int gridHeight = 50;

        [Tooltip("Minimum number of rooms to attempt to place on the floor.")]
        [SerializeField] private int minRoomCount = 5;

        [Tooltip("Maximum number of rooms to attempt to place on the floor.")]
        [SerializeField] private int maxRoomCount = 10;

        [Tooltip("Minimum width of an individual room, in tiles.")]
        [SerializeField] private int minRoomWidth = 4;

        [Tooltip("Maximum width of an individual room, in tiles.")]
        [SerializeField] private int maxRoomWidth = 8;

        [Tooltip("Minimum height of an individual room, in tiles.")]
        [SerializeField] private int minRoomHeight = 4;

        [Tooltip("Maximum height of an individual room, in tiles.")]
        [SerializeField] private int maxRoomHeight = 8;

        [Tooltip("Minimum number of enemies spawned on the floor.")]
        [SerializeField] private int minEnemyCount = 5;

        [Tooltip("Maximum number of enemies spawned on the floor.")]
        [SerializeField] private int maxEnemyCount = 8;

        [Tooltip("Minimum number of items spawned on the floor.")]
        [SerializeField] private int minItemCount = 4;

        [Tooltip("Maximum number of items spawned on the floor.")]
        [SerializeField] private int maxItemCount = 6;

        public DungeonGenerationParams ToParams()
        {
            return new DungeonGenerationParams
            {
                GridWidth = gridWidth,
                GridHeight = gridHeight,
                MinRoomCount = minRoomCount,
                MaxRoomCount = maxRoomCount,
                MinRoomWidth = minRoomWidth,
                MaxRoomWidth = maxRoomWidth,
                MinRoomHeight = minRoomHeight,
                MaxRoomHeight = maxRoomHeight,
                MinEnemyCount = minEnemyCount,
                MaxEnemyCount = maxEnemyCount,
                MinItemCount = minItemCount,
                MaxItemCount = maxItemCount,
            };
        }
    }
}
