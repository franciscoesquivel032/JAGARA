using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;
using Jagara.Runtime.Data;
using Jagara.Runtime.DungeonGen;

namespace Jagara.Tests.EditMode
{
    public class FloorInstantiatorEnvironmentTintTests
    {
        private GameObject instantiatorGO;
        private FloorInstantiator instantiator;
        private GameObject tilemapGO;
        private GameObject decorationTilemapGO;
        private NightmareThemeSO theme;

        [SetUp]
        public void SetUp()
        {
            instantiatorGO = new GameObject("FloorInstantiator");
            instantiator = instantiatorGO.AddComponent<FloorInstantiator>();

            tilemapGO = new GameObject("Tilemap");
            tilemapGO.AddComponent<Tilemap>();
            decorationTilemapGO = new GameObject("DecorationTilemap");
            decorationTilemapGO.AddComponent<Tilemap>();

            SetPrivateField(instantiator, "tilemap", tilemapGO.GetComponent<Tilemap>());
            SetPrivateField(instantiator, "decorationTilemap", decorationTilemapGO.GetComponent<Tilemap>());

            theme = ScriptableObject.CreateInstance<NightmareThemeSO>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(instantiatorGO);
            Object.DestroyImmediate(tilemapGO);
            Object.DestroyImmediate(decorationTilemapGO);
            Object.DestroyImmediate(theme);
        }

        [Test]
        public void ApplyEnvironmentTint_SetsColorOnBothTilemaps()
        {
            instantiator.ApplyEnvironmentTint(theme);

            Assert.AreEqual(theme.EnvironmentTint, instantiator.Tilemap.color);
            Assert.AreEqual(theme.EnvironmentTint, instantiator.DecorationTilemap.color);
        }

        [Test]
        public void ApplyEnvironmentTint_NullTheme_LeavesTilemapsUnchangedAndLogsError()
        {
            var before = instantiator.Tilemap.color;

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("theme is null"));
            instantiator.ApplyEnvironmentTint(null);

            Assert.AreEqual(before, instantiator.Tilemap.color);
        }

        [Test]
        public void ApplyEnvironmentTint_NoDecorationTilemap_StillTintsFloorTilemap()
        {
            SetPrivateField(instantiator, "decorationTilemap", null);

            instantiator.ApplyEnvironmentTint(theme);

            Assert.AreEqual(theme.EnvironmentTint, instantiator.Tilemap.color);
        }

        private static void SetPrivateField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(field, $"field '{name}' not found");
            field.SetValue(target, value);
        }
    }
}
