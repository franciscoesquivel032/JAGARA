using NUnit.Framework;
using Jagara.Runtime.DungeonGen;

namespace Jagara.Tests.EditMode
{
    public class TileVisualResolverWallFillVariantTests
    {
        [Test]
        public void SameInputs_AlwaysReturnSameVariant()
        {
            int first = TileVisualResolver.ResolveWallFillVariant(1234, 7, 11, 25, 25, 25, 25);

            for (int i = 0; i < 10; i++)
            {
                Assert.AreEqual(first, TileVisualResolver.ResolveWallFillVariant(1234, 7, 11, 25, 25, 25, 25));
            }
        }

        [Test]
        public void Result_IsAlwaysAValidVariantIndex()
        {
            for (int x = 0; x < 50; x++)
            {
                for (int y = 0; y < 50; y++)
                {
                    int variant = TileVisualResolver.ResolveWallFillVariant(999, x, y, 70, 15, 10, 5);
                    Assert.GreaterOrEqual(variant, 0);
                    Assert.LessOrEqual(variant, 3);
                }
            }
        }

        [Test]
        public void ObservedFrequencies_ApproximateConfiguredWeights()
        {
            const int size = 200;
            var counts = new int[4];
            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    counts[TileVisualResolver.ResolveWallFillVariant(42, x, y, 40, 30, 20, 10)]++;
                }
            }

            const double total = size * size;
            Assert.AreEqual(0.40, counts[0] / total, 0.03, "variant A frequency");
            Assert.AreEqual(0.30, counts[1] / total, 0.03, "variant B frequency");
            Assert.AreEqual(0.20, counts[2] / total, 0.03, "variant C frequency");
            Assert.AreEqual(0.10, counts[3] / total, 0.03, "variant D frequency");
        }

        [Test]
        public void ZeroWeight_MakesVariantUnreachable()
        {
            for (int x = 0; x < 100; x++)
            {
                for (int y = 0; y < 100; y++)
                {
                    Assert.AreNotEqual(3, TileVisualResolver.ResolveWallFillVariant(7, x, y, 40, 40, 20, 0));
                }
            }
        }

        [Test]
        public void AllZeroWeights_AlwaysReturnsVariantA()
        {
            for (int x = 0; x < 20; x++)
            {
                for (int y = 0; y < 20; y++)
                {
                    Assert.AreEqual(0, TileVisualResolver.ResolveWallFillVariant(7, x, y, 0, 0, 0, 0));
                }
            }
        }

        [Test]
        public void WallFillPattern_IsIndependentFromFloorVariantPattern()
        {
            // Same seed and matching weight setups: the two selectors must not
            // be correlated cell-by-cell (different salts). With identical salts
            // the shared outcomes 0-2 would always agree.
            int differing = 0;
            for (int x = 0; x < 50; x++)
            {
                for (int y = 0; y < 50; y++)
                {
                    int floor = TileVisualResolver.ResolveFloorVariant(42, x, y, 1, 1, 1);
                    int wall = TileVisualResolver.ResolveWallFillVariant(42, x, y, 1, 1, 1, 0);
                    if (floor != wall)
                    {
                        differing++;
                    }
                }
            }

            Assert.Greater(differing, 0, "Wall fill selection is correlated with floor variant selection.");
        }
    }
}
