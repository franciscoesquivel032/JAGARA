using UnityEngine;

namespace Jagara.Runtime.Data
{
    /// <summary>
    /// Static per-enemy-type configuration: display info, movement timing, and
    /// AI wake/forget radii consumed by EnemyAILogic. Combat values are authored
    /// directly (BaseMaxHP / BaseAttackDamage), not derived from Vigor/Poder via
    /// StatFormulas: enemies are hand-tuned per type, so the number in the
    /// Inspector is the number in play. Only the player runs on stats, because
    /// only the player allocates stat points in the hub.
    /// </summary>
    [CreateAssetMenu(fileName = "New Enemy Config", menuName = "Jagara/Enemy Config")]
    public class EnemyConfigSO : ScriptableObject
    {
        [SerializeField] private string displayName;
        [SerializeField] private Sprite sprite;

        [Tooltip("Seconds for a single grid-step move tween (matches GridMover's default).")]
        [SerializeField] private float moveDuration = 0.13f;

        [Tooltip("N: Manhattan radius at which a Dormant enemy wakes and starts chasing.")]
        [SerializeField] private int detectionRange = 6;

        [Tooltip("M: Manhattan radius beyond which a Chasing enemy forgets the player and goes Dormant. Must be >= detectionRange.")]
        [SerializeField] private int forgetRange = 10;

        [Header("Combat")]
        [Tooltip("Max HP for this enemy type. Authored directly - not derived from a stat.")]
        [SerializeField] private int baseMaxHP = 20;

        [Tooltip("Damage this enemy's bump-attack deals. Authored directly - not derived from a stat.")]
        [SerializeField] private int baseAttackDamage = 2;

        public string DisplayName => displayName;
        public Sprite Sprite => sprite;
        public float MoveDuration => moveDuration;
        public int DetectionRange => detectionRange;
        public int ForgetRange => forgetRange;
        public int BaseMaxHP => baseMaxHP;
        public int BaseAttackDamage => baseAttackDamage;

        private void OnValidate()
        {
            // Enforce M >= N to avoid wake/forget hysteresis thrashing: an
            // enemy that would oscillate between Dormant and Chasing every
            // turn because its forget radius is smaller than its detection
            // radius. Clamp forgetRange up to detectionRange.
            if (forgetRange < detectionRange)
            {
                forgetRange = detectionRange;
            }

            // A 0 max-HP enemy spawns already dead: HealthState(0) reports
            // IsDead immediately, but nothing calls Die() on it, so it would sit
            // there blocking its tile for the whole floor. Floor it at 1.
            baseMaxHP = Mathf.Max(1, baseMaxHP);
            baseAttackDamage = Mathf.Max(0, baseAttackDamage);
        }
    }
}
