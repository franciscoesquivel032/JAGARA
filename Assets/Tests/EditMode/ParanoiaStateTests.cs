using NUnit.Framework;
using Jagara.Runtime.Resources;

namespace Jagara.Tests.EditMode
{
    public class ParanoiaStateTests
    {
        [Test]
        public void Constructor_StartsAtZero()
        {
            var paranoia = new ParanoiaState(100);

            Assert.AreEqual(0, paranoia.Current);
            Assert.AreEqual(100, paranoia.Max);
            Assert.IsFalse(paranoia.IsMaxed);
        }

        [Test]
        public void Gain_IncreasesCurrent_AndRaisesOnParanoiaChanged()
        {
            var paranoia = new ParanoiaState(100);
            int? observedCurrent = null;
            paranoia.OnParanoiaChanged += (current, max) => observedCurrent = current;

            paranoia.Gain(3);

            Assert.AreEqual(3, paranoia.Current);
            Assert.AreEqual(3, observedCurrent);
        }

        [Test]
        public void Gain_ClampsAtMax()
        {
            var paranoia = new ParanoiaState(10);

            paranoia.Gain(999);

            Assert.AreEqual(10, paranoia.Current);
            Assert.IsTrue(paranoia.IsMaxed);
        }

        [Test]
        public void Gain_ReachingMax_RaisesOnParanoiaMaxedExactlyOnce()
        {
            var paranoia = new ParanoiaState(10);
            int maxedCount = 0;
            paranoia.OnParanoiaMaxed += () => maxedCount++;

            paranoia.Gain(10);
            Assert.AreEqual(1, maxedCount);

            // Already maxed - further Gain calls must not re-fire OnParanoiaMaxed.
            paranoia.Gain(5);
            Assert.AreEqual(1, maxedCount);
        }

        [Test]
        public void Gain_AfterMaxed_IsNoOp()
        {
            var paranoia = new ParanoiaState(10);
            paranoia.Gain(10);

            bool changedAfterMax = false;
            paranoia.OnParanoiaChanged += (_, _) => changedAfterMax = true;
            paranoia.Gain(1);

            Assert.IsFalse(changedAfterMax);
            Assert.AreEqual(10, paranoia.Current);
        }

        [Test]
        public void Gain_NonPositiveAmount_IsNoOp()
        {
            var paranoia = new ParanoiaState(100);
            bool changed = false;
            paranoia.OnParanoiaChanged += (_, _) => changed = true;

            paranoia.Gain(0);
            paranoia.Gain(-5);

            Assert.AreEqual(0, paranoia.Current);
            Assert.IsFalse(changed);
        }
    }
}
