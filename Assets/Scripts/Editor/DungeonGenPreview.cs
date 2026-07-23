using Jagara.Runtime.Data;
using Jagara.Runtime.DungeonGen;
using UnityEditor;
using UnityEditor.SceneManagement;
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

        [MenuItem("Tools/Jāgara/Generate Preview Floor In Scene")]
        private static void GeneratePreviewFloorInScene()
        {
            var paramsSO = AssetDatabase.LoadAssetAtPath<DungeonGenerationParamsSO>(ParamsAssetPath);
            if (paramsSO == null)
            {
                Debug.LogError($"Could not load DungeonGenerationParamsSO at '{ParamsAssetPath}'.");
                return;
            }

            var instantiator = Object.FindFirstObjectByType<FloorInstantiator>();
            if (instantiator == null)
            {
                Debug.LogError("No FloorInstantiator found in the currently open scene. " +
                                "Open Assets/Scenes/DungeonPreview.unity and try again.");
                return;
            }

            int seed = new System.Random().Next();
            var floor = new DungeonGenerator().GenerateFloor(seed, paramsSO.ToParams());
            instantiator.InstantiateFloor(floor);

            EditorSceneManager.MarkSceneDirty(instantiator.gameObject.scene);

            Debug.Log($"Dungeon floor generated in scene — seed: {seed}\n{floor.ToAsciiArt()}");
        }
    }
}
