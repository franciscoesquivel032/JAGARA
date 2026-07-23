using Jagara.Runtime.Data;
using Jagara.Runtime.DungeonGen;
using UnityEditor;
using UnityEngine;

namespace Jagara.Editor.DungeonGen
{
    public static class DungeonGenPreview
    {
        private const string ParamsAssetPath = "Assets/ScriptableObjects/DungeonGenParams_Nightmare1.asset";

        [MenuItem("Tools/Jāgara/Preview Dungeon Floor")]
        private static void PreviewDungeonFloor()
        {
            var paramsSO = AssetDatabase.LoadAssetAtPath<DungeonGenerationParamsSO>(ParamsAssetPath);
            if (paramsSO == null)
            {
                Debug.LogError($"Could not load DungeonGenerationParamsSO at '{ParamsAssetPath}'.");
                return;
            }

            int seed = new System.Random().Next();
            var floor = new DungeonGenerator().GenerateFloor(seed, paramsSO.ToParams());

            Debug.Log($"Dungeon floor preview — seed: {seed}\n{floor.ToAsciiArt()}");
        }
    }
}
