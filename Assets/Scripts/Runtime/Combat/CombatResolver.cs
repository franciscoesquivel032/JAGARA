using Jagara.Runtime.Resources;

namespace Jagara.Runtime.Combat
{
    /// <summary>
    /// Resolves a basic bump-attack (no PP cost - the ability/PP menu is
    /// future work) between an attacker's Poder and a defender's HealthState.
    /// Pure C#, no MonoBehaviour dependency - shared by PlayerController and
    /// EnemyController.
    /// </summary>
    public static class CombatResolver
    {
        public readonly struct AttackResult
        {
            public readonly int Damage;
            public readonly bool DefenderDied;

            public AttackResult(int damage, bool defenderDied)
            {
                Damage = damage;
                DefenderDied = defenderDied;
            }
        }

        public static AttackResult ResolveBumpAttack(int attackerPoder, HealthState defenderHealth)
        {
            int damage = StatFormulas.ComputeAttackDamage(attackerPoder);
            defenderHealth.TakeDamage(damage);
            return new AttackResult(damage, defenderHealth.IsDead);
        }
    }
}
