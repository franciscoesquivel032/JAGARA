using NUnit.Framework;
using UnityEngine;
using Jagara.Runtime.UI;

namespace Jagara.Tests.EditMode
{
    public class HpPopupFormatterTests
    {
        [Test]
        public void Format_NegativeDelta_ReturnsDamageTextInTheSuppliedColor()
        {
            var result = HpPopupFormatter.Format(-8, Color.red);

            Assert.IsTrue(result.HasValue);
            Assert.AreEqual("-8", result.Value.Text);
            Assert.AreEqual(Color.red, result.Value.Color);
        }

        [Test]
        public void Format_NegativeDelta_UsesWhateverDamageColorItsCallerPasses()
        {
            // The caller (HpPopupBinder) picks white on an enemy vs red on
            // the player - Format itself must not hardcode either.
            var result = HpPopupFormatter.Format(-3, Color.white);

            Assert.AreEqual(Color.white, result.Value.Color);
        }

        [Test]
        public void Format_PositiveDelta_ReturnsHealTextAndColor_RegardlessOfDamageColor()
        {
            var result = HpPopupFormatter.Format(5, Color.red);

            Assert.IsTrue(result.HasValue);
            Assert.AreEqual("+5", result.Value.Text);
            Assert.AreEqual(HpPopupFormatter.HealColor, result.Value.Color);
        }

        [Test]
        public void Format_ZeroDelta_ReturnsNull()
        {
            var result = HpPopupFormatter.Format(0, Color.white);

            Assert.IsFalse(result.HasValue);
        }

        [Test]
        public void Format_OverkillDelta_ReportsTheHPActuallyLost()
        {
            // Mirrors HealthState.TakeDamage's overkill behavior: the delta
            // passed in must already be the HP actually removed, not the
            // rolled damage - HpPopupBinder computes it from Current, which
            // HealthState itself already clamps at 0.
            var result = HpPopupFormatter.Format(-5, Color.red);

            Assert.AreEqual("-5", result.Value.Text);
        }
    }
}
