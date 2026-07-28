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
using Jagara.Runtime.Enemies;
using Jagara.Runtime.Gameplay;

namespace Jagara.Tests.PlayMode
{
    public class NightmareBootstrapTests
    {
        private const string ScenePath = "Assets/Scenes/DungeonPreview.unity";
        private const string ParamsAssetPath = "Assets/ScriptableObjects/DungeonGenParams_Nightmare1.asset";
        private const string ThemeAssetPath = "Assets/ScriptableObjects/Themes/DefaultNightmareTheme.asset";

        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly Color PlayerOutlineColor = new Color(194f / 255f, 168f / 255f, 116f / 255f, 1f);
        private static readonly Color EnemyOutlineColor = new Color(168f / 255f, 92f / 255f, 82f / 255f, 1f);

        [UnityTest]
        public IEnumerator NightmareBootstrap_OnStart_InstantiatesFloorWithinConfiguredRanges()
        {
            EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;

            var tilemap = Object.FindFirstObjectByType<Tilemap>();
            Assert.IsNotNull(tilemap, "Expected a Tilemap in the scene.");
            Assert.Greater(tilemap.GetUsedTilesCount(), 0, "Expected at least one painted tile.");

            int enemyCount = Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None).Length;
            int itemCount = Object.FindObjectsByType<ItemMarker>(FindObjectsSortMode.None).Length;

            var paramsSO = AssetDatabase.LoadAssetAtPath<DungeonGenerationParamsSO>(ParamsAssetPath);
            Assert.IsNotNull(paramsSO, $"Expected a DungeonGenerationParamsSO asset at '{ParamsAssetPath}'.");
            var configuredParams = paramsSO.ToParams();

            Assert.That(enemyCount, Is.InRange(configuredParams.MinEnemyCount, configuredParams.MaxEnemyCount),
                $"EnemyController count {enemyCount} outside configured range [{configuredParams.MinEnemyCount}, {configuredParams.MaxEnemyCount}].");
            Assert.That(itemCount, Is.InRange(configuredParams.MinItemCount, configuredParams.MaxItemCount),
                $"ItemMarker count {itemCount} outside configured range [{configuredParams.MinItemCount}, {configuredParams.MaxItemCount}].");
        }

        [UnityTest]
        public IEnumerator NightmareBootstrap_OnStart_AppliesEnvironmentTintToBothTilemaps()
        {
            EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;

            var floorInstantiator = Object.FindFirstObjectByType<FloorInstantiator>();
            Assert.IsNotNull(floorInstantiator, "Expected a FloorInstantiator in the scene.");

            var theme = AssetDatabase.LoadAssetAtPath<NightmareThemeSO>(ThemeAssetPath);
            Assert.IsNotNull(theme, $"Expected a NightmareThemeSO asset at '{ThemeAssetPath}'.");

            Assert.AreEqual(theme.EnvironmentTint, floorInstantiator.Tilemap.color,
                "Expected the floor tilemap's color to match the theme's EnvironmentTint.");
            Assert.AreEqual(theme.EnvironmentTint, floorInstantiator.DecorationTilemap.color,
                "Expected the decoration tilemap's color to match the theme's EnvironmentTint.");
        }

        [UnityTest]
        public IEnumerator NightmareBootstrap_OnStart_SpawnsPlayerAndEnemiesWithFactionOutlineColors()
        {
            EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;

            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.IsNotNull(player, "Expected a PlayerController in the scene.");
            AssertOutlineColor(player.transform, PlayerOutlineColor, "Player");

            var enemies = Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
            Assert.Greater(enemies.Length, 0, "Expected at least one spawned enemy.");
            foreach (var enemy in enemies)
            {
                AssertOutlineColor(enemy.transform, EnemyOutlineColor, "Enemy");
            }
        }

        private static void AssertOutlineColor(Transform root, Color expected, string label)
        {
            var spriteRenderer = root.GetComponentInChildren<SpriteRenderer>();
            Assert.IsNotNull(spriteRenderer, $"Expected a SpriteRenderer under {label}.");

            var block = new MaterialPropertyBlock();
            spriteRenderer.GetPropertyBlock(block);
            Color actual = block.GetColor(OutlineColorId);

            // Per-channel comparison with a small tolerance (not exact Color equality):
            // MaterialPropertyBlock.SetColor/GetColor round-trips shader-reflected Color
            // properties through a gamma<->linear conversion under Linear color space,
            // which can introduce sub-ULP float noise even when the stored value is
            // bit-exact. Same convention as Assets/Tests/EditMode/EntityOutlineTests.cs.
            const float tolerance = 0.001f;
            Assert.AreEqual(expected.r, actual.r, tolerance, $"{label} outline color mismatch (r).");
            Assert.AreEqual(expected.g, actual.g, tolerance, $"{label} outline color mismatch (g).");
            Assert.AreEqual(expected.b, actual.b, tolerance, $"{label} outline color mismatch (b).");
            Assert.AreEqual(expected.a, actual.a, tolerance, $"{label} outline color mismatch (a).");
        }

    }
}
