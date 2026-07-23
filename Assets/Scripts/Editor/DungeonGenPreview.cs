using System;
using Jagara.Runtime.DungeonGen;
using UnityEditor;
using UnityEngine;

namespace Jagara.Editor.DungeonGen
{
    public static class DungeonGenPreview
    {
        private static readonly DungeonGenerationParams DefaultParams = new DungeonGenerationParams
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

        [MenuItem("Tools/Jāgara/Preview Dungeon Floor")]
        private static void PreviewDungeonFloor()
        {
            int seed = new System.Random().Next();
            var floor = new DungeonGenerator().GenerateFloor(seed, DefaultParams);

            Debug.Log($"Dungeon floor preview — seed: {seed}\n{floor.ToAsciiArt()}");
        }
    }
}
