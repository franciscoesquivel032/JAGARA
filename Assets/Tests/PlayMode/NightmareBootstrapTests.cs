using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;
using Jagara.Runtime.Data;
using Jagara.Runtime.DungeonGen;

namespace Jagara.Tests.PlayMode
{
    public class NightmareBootstrapTests
    {
        private const string ScenePath = "Assets/Scenes/DungeonPreview.unity";
        private const string ParamsAssetPath = "Assets/ScriptableObjects/DungeonGenParams_Nightmare1.asset";

        [UnityTest]
        public IEnumerator NightmareBootstrap_OnStart_InstantiatesFloorWithinConfiguredRanges()
        {
            EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;

            var tilemap = Object.FindFirstObjectByType<Tilemap>();
            Assert.IsNotNull(tilemap, "Expected a Tilemap in the scene.");
            Assert.Greater(tilemap.GetUsedTilesCount(), 0, "Expected at least one painted tile.");

            var floorInstantiator = Object.FindFirstObjectByType<FloorInstantiator>();
            Assert.IsNotNull(floorInstantiator, "Expected a FloorInstantiator in the scene.");

            Transform spawnedEntities = floorInstantiator.transform.Find("SpawnedEntities");
            Assert.IsNotNull(spawnedEntities, "Expected FloorInstantiator to have spawned a SpawnedEntities root.");

            string enemyPrefabName = GetPrefabName(floorInstantiator, "enemyMarkerPrefab");
            string itemPrefabName = GetPrefabName(floorInstantiator, "itemMarkerPrefab");

            int enemyCount = CountChildrenNamed(spawnedEntities, enemyPrefabName);
            int itemCount = CountChildrenNamed(spawnedEntities, itemPrefabName);

            var paramsSO = AssetDatabase.LoadAssetAtPath<DungeonGenerationParamsSO>(ParamsAssetPath);
            Assert.IsNotNull(paramsSO, $"Expected a DungeonGenerationParamsSO asset at '{ParamsAssetPath}'.");
            var configuredParams = paramsSO.ToParams();

            Assert.That(enemyCount, Is.InRange(configuredParams.MinEnemyCount, configuredParams.MaxEnemyCount),
                $"Enemy marker count {enemyCount} outside configured range [{configuredParams.MinEnemyCount}, {configuredParams.MaxEnemyCount}].");
            Assert.That(itemCount, Is.InRange(configuredParams.MinItemCount, configuredParams.MaxItemCount),
                $"Item marker count {itemCount} outside configured range [{configuredParams.MinItemCount}, {configuredParams.MaxItemCount}].");
        }

        private static string GetPrefabName(FloorInstantiator instantiator, string fieldName)
        {
            var serialized = new SerializedObject(instantiator);
            var property = serialized.FindProperty(fieldName);
            Assert.IsNotNull(property, $"Expected serialized field '{fieldName}' on FloorInstantiator.");
            var prefab = property.objectReferenceValue as GameObject;
            Assert.IsNotNull(prefab, $"Expected '{fieldName}' to be assigned on FloorInstantiator.");
            return prefab.name;
        }

        private static int CountChildrenNamed(Transform parent, string prefabName)
        {
            string cloneName = $"{prefabName}(Clone)";
            int count = 0;
            for (int i = 0; i < parent.childCount; i++)
            {
                if (parent.GetChild(i).name == cloneName)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
