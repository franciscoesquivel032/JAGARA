using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;
using Jagara.Runtime.Data;

namespace Jagara.Tests.EditMode
{
    public class DungeonTilesetSOTests
    {
        private DungeonTilesetSO tileset;

        [SetUp]
        public void SetUp()
        {
            tileset = ScriptableObject.CreateInstance<DungeonTilesetSO>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(tileset);
        }

        [Test]
        public void WallFillWeights_DefaultToEqualDistribution()
        {
            Assert.AreEqual(25, tileset.WallFillWeightA);
            Assert.AreEqual(25, tileset.WallFillWeightB);
            Assert.AreEqual(25, tileset.WallFillWeightC);
            Assert.AreEqual(25, tileset.WallFillWeightD);
        }

        [Test]
        public void Decorations_DefaultToEmptyWithModestDensity()
        {
            Assert.AreEqual(0, tileset.Decorations.Count);
            Assert.AreEqual(10, tileset.DecorationDensityPercent);
        }

        [Test]
        public void GetWallFillVariantTile_UnassignedVariants_FallBackToVariantA()
        {
            var fillA = ScriptableObject.CreateInstance<Tile>();
            try
            {
                SetPrivateField(tileset, "wallFill", fillA);

                for (int i = 0; i < 4; i++)
                {
                    Assert.AreSame(fillA, tileset.GetWallFillVariantTile(i), $"variant {i}");
                }
            }
            finally
            {
                Object.DestroyImmediate(fillA);
            }
        }

        [Test]
        public void GetWallFillVariantTile_AssignedVariant_IsReturned()
        {
            var fillA = ScriptableObject.CreateInstance<Tile>();
            var fillB = ScriptableObject.CreateInstance<Tile>();
            try
            {
                SetPrivateField(tileset, "wallFill", fillA);
                SetPrivateField(tileset, "wallFillB", fillB);

                Assert.AreSame(fillB, tileset.GetWallFillVariantTile(1));
                Assert.AreSame(fillA, tileset.GetWallFillVariantTile(2), "unassigned C falls back");
            }
            finally
            {
                Object.DestroyImmediate(fillA);
                Object.DestroyImmediate(fillB);
            }
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
