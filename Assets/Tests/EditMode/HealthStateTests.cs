using NUnit.Framework;
using Jagara.Runtime.Resources;

namespace Jagara.Tests.EditMode
{
    public class HealthStateTests
    {
        [Test]
        public void Constructor_SetsCurrentToMax()
        {
            var health = new HealthState(20);

            Assert.AreEqual(20, health.Current);
            Assert.AreEqual(20, health.Max);
            Assert.IsFalse(health.IsDead);
        }

        [Test]
        public void TakeDamage_ReducesCurrent_AndRaisesOnHPChanged()
        {
            var health = new HealthState(10);
            int? observedCurrent = null;
            int? observedMax = null;
            health.OnHPChanged += (current, max) => { observedCurrent = current; observedMax = max; };

            health.TakeDamage(4);

            Assert.AreEqual(6, health.Current);
            Assert.AreEqual(6, observedCurrent);
            Assert.AreEqual(10, observedMax);
        }

        [Test]
        public void TakeDamage_ClampsAtZero_AndDoesNotGoNegative()
        {
            var health = new HealthState(5);

            health.TakeDamage(999);

            Assert.AreEqual(0, health.Current);
        }

        [Test]
        public void TakeDamage_LethalAmount_SetsIsDeadAndRaisesOnDeath()
        {
            var health = new HealthState(5);
            bool died = false;
            health.OnDeath += () => died = true;

            health.TakeDamage(5);

            Assert.IsTrue(health.IsDead);
            Assert.IsTrue(died);
        }

        [Test]
        public void TakeDamage_AfterDeath_IsNoOp()
        {
            var health = new HealthState(5);
            health.TakeDamage(5);

            int deathCount = 0;
            health.OnDeath += () => deathCount++;

            health.TakeDamage(1);

            Assert.AreEqual(0, deathCount);
            Assert.AreEqual(0, health.Current);
        }

        [Test]
        public void TakeDamage_NonPositiveAmount_IsNoOp()
        {
            var health = new HealthState(10);
            bool changed = false;
            health.OnHPChanged += (_, _) => changed = true;

            health.TakeDamage(0);
            health.TakeDamage(-5);

            Assert.AreEqual(10, health.Current);
            Assert.IsFalse(changed);
        }

        // The returned figure is what combat logs report. It must track the health
        // bar exactly, or a line reading "hits for 6" appears over a 2 HP drop.

        [Test]
        public void TakeDamage_ReturnsTheHPActuallyRemoved()
        {
            var health = new HealthState(10);

            Assert.AreEqual(4, health.TakeDamage(4));
        }

        [Test]
        public void TakeDamage_Overkill_ReturnsOnlyTheHPThatWasLeft()
        {
            var health = new HealthState(5);

            Assert.AreEqual(5, health.TakeDamage(999), "An overkill hit must report the 5 HP lost, not the 999 rolled.");
        }

        [Test]
        public void TakeDamage_AfterDeath_ReturnsZero()
        {
            var health = new HealthState(5);
            health.TakeDamage(5);

            Assert.AreEqual(0, health.TakeDamage(3));
        }

        [Test]
        public void TakeDamage_NonPositiveAmount_ReturnsZero()
        {
            var health = new HealthState(10);

            Assert.AreEqual(0, health.TakeDamage(0));
            Assert.AreEqual(0, health.TakeDamage(-5));
        }
    }
}
