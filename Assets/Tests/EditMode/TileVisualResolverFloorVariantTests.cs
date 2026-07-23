using NUnit.Framework;
using Jagara.Runtime.DungeonGen;

namespace Jagara.Tests.EditMode
{
    public class TileVisualResolverFloorVariantTests
    {
        [Test]
        public void SameInputs_AlwaysReturnSameVariant()
        {
            int first = TileVisualResolver.ResolveFloorVariant(1234, 7, 11, 75, 20, 5);

            for (int i = 0; i < 10; i++)
            {
                Assert.AreEqual(first, TileVisualResolver.ResolveFloorVariant(1234, 7, 11, 75, 20, 5));
            }
        }

        [Test]
        public void DifferentSeeds_ProduceDifferentPatterns()
        {
            int differing = 0;
            for (int x = 0; x < 20; x++)
            {
                for (int y = 0; y < 20; y++)
                {
                    int a = TileVisualResolver.ResolveFloorVariant(1, x, y, 75, 20, 5);
                    int b = TileVisualResolver.ResolveFloorVariant(2, x, y, 75, 20, 5);
                    if (a != b)
                    {
                        differing++;
                    }
                }
            }

            Assert.Greater(differing, 0,
                "Two different seeds produced identical variant patterns over a 20x20 grid.");
        }

        [Test]
        public void Result_IsAlwaysAValidVariantIndex()
        {
            for (int x = 0; x < 50; x++)
            {
                for (int y = 0; y < 50; y++)
                {
                    int variant = TileVisualResolver.ResolveFloorVariant(999, x, y, 75, 20, 5);
                    Assert.GreaterOrEqual(variant, 0);
                    Assert.LessOrEqual(variant, 2);
                }
            }
        }

        [Test]
        public void ObservedFrequencies_ApproximateConfiguredWeights()
        {
            const int size = 200;
            var counts = new int[3];
            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    counts[TileVisualResolver.ResolveFloorVariant(42, x, y, 75, 20, 5)]++;
                }
            }

            const double total = size * size;
            Assert.AreEqual(0.75, counts[0] / total, 0.03, "primary frequency");
            Assert.AreEqual(0.20, counts[1] / total, 0.03, "secondary frequency");
            Assert.AreEqual(0.05, counts[2] / total, 0.03, "tertiary frequency");
        }

        [Test]
        public void ZeroTertiaryWeight_NeverReturnsTertiary()
        {
            for (int x = 0; x < 100; x++)
            {
                for (int y = 0; y < 100; y++)
                {
                    Assert.AreNotEqual(2, TileVisualResolver.ResolveFloorVariant(7, x, y, 80, 20, 0));
                }
            }
        }

        [Test]
        public void AllZeroWeights_AlwaysReturnsPrimary()
        {
            for (int x = 0; x < 20; x++)
            {
                for (int y = 0; y < 20; y++)
                {
                    Assert.AreEqual(0, TileVisualResolver.ResolveFloorVariant(7, x, y, 0, 0, 0));
                }
            }
        }
    }
}
