using Jagara.Runtime.Resources;

namespace Jagara.Runtime.Combat
{
    /// <summary>
    /// Resolves a basic bump-attack (no PP cost - the ability/PP menu is
    /// future work) between an already-computed attack damage and a defender's
    /// HealthState. It takes the final damage rather than a stat, because the
    /// two attackers derive it differently: the player runs Poder through
    /// StatFormulas, while an enemy reads EnemyConfigSO.BaseAttackDamage
    /// directly. Pure C#, no MonoBehaviour dependency - shared by
    /// PlayerController and EnemyController.
    /// </summary>
    public static class CombatResolver
    {
        public readonly struct AttackResult
        {
            /// <summary>
            /// HP the defender actually lost - not the rolled figure. They differ on
            /// an overkill hit, and this is the one that gets logged, so the number
            /// on screen always matches the health bar's drop.
            /// </summary>
            public readonly int Damage;

            public readonly bool DefenderDied;

            public AttackResult(int damage, bool defenderDied)
            {
                Damage = damage;
                DefenderDied = defenderDied;
            }
        }

        public static AttackResult ResolveBumpAttack(int attackDamage, HealthState defenderHealth)
        {
            int applied = defenderHealth.TakeDamage(attackDamage);
            return new AttackResult(applied, defenderHealth.IsDead);
        }
    }
}
