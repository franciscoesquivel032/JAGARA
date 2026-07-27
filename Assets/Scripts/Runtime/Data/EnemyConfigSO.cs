using UnityEngine;

namespace Jagara.Runtime.Data
{
    /// <summary>
    /// Static per-enemy-type configuration: display info, movement timing, and
    /// AI wake/forget radii consumed by EnemyAILogic. Combat stats are inert
    /// placeholder data - there is no combat system yet.
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

        [Header("Combat (inert data - no combat system yet)")]
        [SerializeField] private int maxHP;
        [SerializeField] private int attackPower;
        [SerializeField] private int defense;

        public string DisplayName => displayName;
        public Sprite Sprite => sprite;
        public float MoveDuration => moveDuration;
        public int DetectionRange => detectionRange;
        public int ForgetRange => forgetRange;
        public int MaxHP => maxHP;
        public int AttackPower => attackPower;
        public int Defense => defense;

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
        }
    }
}
