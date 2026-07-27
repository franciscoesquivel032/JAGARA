using System;
using NUnit.Framework;
using Jagara.Runtime.Enemies;

namespace Jagara.Tests.EditMode
{
    public class EnemyRosterPickerTests
    {
        [Test]
        public void PickIndex_NullWeights_ReturnsNegativeOne()
        {
            Assert.AreEqual(-1, EnemyRosterPicker.PickIndex(null, new Random(1)));
        }

        [Test]
        public void PickIndex_EmptyWeights_ReturnsNegativeOne()
        {
            Assert.AreEqual(-1, EnemyRosterPicker.PickIndex(Array.Empty<int>(), new Random(1)));
        }

        [Test]
        public void PickIndex_AllZeroWeights_ReturnsNegativeOne()
        {
            var weights = new[] { 0, 0, 0 };
            Assert.AreEqual(-1, EnemyRosterPicker.PickIndex(weights, new Random(1)));
        }

        [Test]
        public void PickIndex_SameSeed_ProducesIdenticalSequence()
        {
            var weights = new[] { 3, 5, 2, 7 };

            var rngA = new Random(1234);
            var rngB = new Random(1234);

            for (int i = 0; i < 50; i++)
            {
                int a = EnemyRosterPicker.PickIndex(weights, rngA);
                int b = EnemyRosterPicker.PickIndex(weights, rngB);
                Assert.AreEqual(a, b, $"draw {i} diverged between two Random(1234) instances");
            }
        }

        [Test]
        public void PickIndex_DifferentSeeds_CanProduceDifferentSequences()
        {
            var weights = new[] { 3, 5, 2, 7 };

            var rngA = new Random(1);
            var rngB = new Random(2);

            bool sawDifference = false;
            for (int i = 0; i < 50; i++)
            {
                int a = EnemyRosterPicker.PickIndex(weights, rngA);
                int b = EnemyRosterPicker.PickIndex(weights, rngB);
                if (a != b)
                {
                    sawDifference = true;
                    break;
                }
            }

            Assert.IsTrue(sawDifference, "expected different seeds to diverge at least once across 50 draws");
        }

        [Test]
        public void PickIndex_ZeroWeightEntry_IsNeverSelected()
        {
            var weights = new[] { 0, 5 };
            var rng = new Random(42);

            for (int i = 0; i < 200; i++)
            {
                int index = EnemyRosterPicker.PickIndex(weights, rng);
                Assert.AreEqual(1, index, $"draw {i} selected the zero-weight entry");
            }
        }

        [Test]
        public void PickIndex_MixedZeroWeights_OnlySelectsNonZeroIndices()
        {
            var weights = new[] { 0, 4, 0, 6, 0 };
            var rng = new Random(7);

            for (int i = 0; i < 200; i++)
            {
                int index = EnemyRosterPicker.PickIndex(weights, rng);
                Assert.IsTrue(index == 1 || index == 3, $"draw {i} returned unexpected index {index}");
            }
        }

        [Test]
        public void PickIndex_SingleNonZeroWeight_AlwaysSelectsIt()
        {
            var weights = new[] { 0, 0, 9 };
            var rng = new Random(99);

            for (int i = 0; i < 20; i++)
            {
                Assert.AreEqual(2, EnemyRosterPicker.PickIndex(weights, rng));
            }
        }
    }
}
