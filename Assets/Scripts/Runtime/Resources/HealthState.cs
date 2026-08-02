using System;

namespace Jagara.Runtime.Resources
{
    /// <summary>
    /// Runtime HP for a single entity (player or one enemy instance). Pure C# -
    /// not a ScriptableObject, since multiple enemies sharing one EnemyConfigSO
    /// asset each need independent current HP; not a MonoBehaviour, since it
    /// has no lifecycle of its own - owners (PlayerStatsSO, EnemyController)
    /// create and hold one instance each.
    /// </summary>
    public class HealthState
    {
        public int Current { get; private set; }
        public int Max { get; }

        public bool IsDead => Current <= 0;

        public event Action<int, int> OnHPChanged = delegate { };
        public event Action OnDeath = delegate { };

        public HealthState(int max)
        {
            Max = max;
            Current = max;
        }

        /// <summary>Applies damage, clamped at 0. No-ops if already dead or amount is non-positive.</summary>
        public void TakeDamage(int amount)
        {
            if (amount <= 0 || IsDead)
            {
                return;
            }

            Current = Math.Max(0, Current - amount);
            OnHPChanged(Current, Max);

            if (IsDead)
            {
                OnDeath();
            }
        }
    }
}
