using NUnit.Framework;
using Jagara.Runtime.DungeonGen;

namespace Jagara.Tests.EditMode
{
    public class TileVisualResolverDecorationTests
    {
        private static readonly int[] DefaultWeights = { 50, 30, 20 };

        [Test]
        public void SameInputs_AlwaysReturnSameResult()
        {
            int first = TileVisualResolver.ResolveDecorationIndex(1234, 7, 11, 30, DefaultWeights);

            for (int i = 0; i < 10; i++)
            {
                Assert.AreEqual(first, TileVisualResolver.ResolveDecorationIndex(1234, 7, 11, 30, DefaultWeights));
            }
        }

        [Test]
        public void ZeroDensity_NeverDecorates()
        {
            for (int x = 0; x < 50; x++)
            {
                for (int y = 0; y < 50; y++)
                {
                    Assert.AreEqual(-1, TileVisualResolver.ResolveDecorationIndex(7, x, y, 0, DefaultWeights));
                }
            }
        }

        [Test]
        public void FullDensity_AlwaysDecorates()
        {
            for (int x = 0; x < 50; x++)
            {
                for (int y = 0; y < 50; y++)
                {
                    Assert.GreaterOrEqual(TileVisualResolver.ResolveDecorationIndex(7, x, y, 100, DefaultWeights), 0);
                }
            }
        }

        [Test]
        public void NullOrEmptyWeights_NeverDecorates()
        {
            Assert.AreEqual(-1, TileVisualResolver.ResolveDecorationIndex(7, 3, 4, 100, null));
            Assert.AreEqual(-1, TileVisualResolver.ResolveDecorationIndex(7, 3, 4, 100, new int[0]));
        }

        [Test]
        public void AllZeroWeights_NeverDecorates()
        {
            Assert.AreEqual(-1, TileVisualResolver.ResolveDecorationIndex(7, 3, 4, 100, new[] { 0, 0 }));
        }

        [Test]
        public void Result_IsAlwaysMinusOneOrValidIndex()
        {
            for (int x = 0; x < 50; x++)
            {
                for (int y = 0; y < 50; y++)
                {
                    int index = TileVisualResolver.ResolveDecorationIndex(999, x, y, 35, DefaultWeights);
                    Assert.GreaterOrEqual(index, -1);
                    Assert.Less(index, DefaultWeights.Length);
                }
            }
        }

        [Test]
        public void ObservedDecorationRate_ApproximatesDensity()
        {
            const int size = 200;
            int decorated = 0;
            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    if (TileVisualResolver.ResolveDecorationIndex(42, x, y, 30, DefaultWeights) >= 0)
                    {
                        decorated++;
                    }
                }
            }

            Assert.AreEqual(0.30, decorated / (double)(size * size), 0.03, "decoration rate");
        }

        [Test]
        public void ZeroWeightEntry_IsUnreachable()
        {
            var weights = new[] { 60, 0, 40 };
            for (int x = 0; x < 100; x++)
            {
                for (int y = 0; y < 100; y++)
                {
                    Assert.AreNotEqual(1, TileVisualResolver.ResolveDecorationIndex(7, x, y, 100, weights));
                }
            }
        }
    }
}
