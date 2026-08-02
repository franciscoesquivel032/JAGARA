using NUnit.Framework;
using Jagara.Runtime.Combat;
using Jagara.Runtime.Resources;

namespace Jagara.Tests.EditMode
{
    public class CombatResolverTests
    {
        [Test]
        public void ResolveBumpAttack_AppliesComputedDamageToDefender()
        {
            var defenderHealth = new HealthState(50);

            CombatResolver.AttackResult result = CombatResolver.ResolveBumpAttack(3, defenderHealth);

            Assert.AreEqual(StatFormulas.ComputeAttackDamage(3), result.Damage);
            Assert.AreEqual(50 - result.Damage, defenderHealth.Current);
        }

        [Test]
        public void ResolveBumpAttack_LethalDamage_ReportsDefenderDied()
        {
            var defenderHealth = new HealthState(1);

            CombatResolver.AttackResult result = CombatResolver.ResolveBumpAttack(0, defenderHealth);

            Assert.IsTrue(result.DefenderDied);
            Assert.IsTrue(defenderHealth.IsDead);
        }

        [Test]
        public void ResolveBumpAttack_NonLethalDamage_ReportsDefenderDidNotDie()
        {
            var defenderHealth = new HealthState(1000);

            CombatResolver.AttackResult result = CombatResolver.ResolveBumpAttack(0, defenderHealth);

            Assert.IsFalse(result.DefenderDied);
        }
    }
}
