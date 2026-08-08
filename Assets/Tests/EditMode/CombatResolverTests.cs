using NUnit.Framework;
using Jagara.Runtime.Combat;
using Jagara.Runtime.Resources;

namespace Jagara.Tests.EditMode
{
    public class CombatResolverTests
    {
        [Test]
        public void ResolveBumpAttack_AppliesGivenDamageToDefender()
        {
            var defenderHealth = new HealthState(50);

            CombatResolver.AttackResult result = CombatResolver.ResolveBumpAttack(5, defenderHealth);

            // The resolver applies the damage it is handed, unchanged - it does
            // not run it through StatFormulas, because enemies author their
            // damage directly and only the player derives it from a stat.
            Assert.AreEqual(5, result.Damage);
            Assert.AreEqual(45, defenderHealth.Current);
        }

        [Test]
        public void ResolveBumpAttack_LethalDamage_ReportsDefenderDied()
        {
            var defenderHealth = new HealthState(1);

            CombatResolver.AttackResult result = CombatResolver.ResolveBumpAttack(1, defenderHealth);

            Assert.IsTrue(result.DefenderDied);
            Assert.IsTrue(defenderHealth.IsDead);
        }

        [Test]
        public void ResolveBumpAttack_NonLethalDamage_ReportsDefenderDidNotDie()
        {
            var defenderHealth = new HealthState(1000);

            CombatResolver.AttackResult result = CombatResolver.ResolveBumpAttack(1, defenderHealth);

            Assert.IsFalse(result.DefenderDied);
        }

        [Test]
        public void ResolveBumpAttack_Overkill_ReportsDamageActuallyApplied()
        {
            // 1 HP left, incoming damage is 2 - the log line must say 1, matching
            // the health bar, not the 2 the attacker swung for.
            var defenderHealth = new HealthState(1);

            CombatResolver.AttackResult result = CombatResolver.ResolveBumpAttack(2, defenderHealth);

            Assert.AreEqual(1, result.Damage);
        }

        [Test]
        public void ResolveBumpAttack_AlreadyDeadDefender_AppliesAndReportsNothing()
        {
            var defenderHealth = new HealthState(1);
            CombatResolver.ResolveBumpAttack(1, defenderHealth);
            Assert.IsTrue(defenderHealth.IsDead, "Precondition: the defender should already be dead.");

            CombatResolver.AttackResult result = CombatResolver.ResolveBumpAttack(5, defenderHealth);

            Assert.AreEqual(0, result.Damage, "Hitting a corpse must not report damage it did not deal.");
            Assert.AreEqual(0, defenderHealth.Current);
        }
    }
}
