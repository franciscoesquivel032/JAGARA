using UnityEngine;
using Jagara.Runtime.Combat;
using Jagara.Runtime.Resources;

namespace Jagara.Runtime.Data
{
    /// <summary>
    /// The player's stats: Vigor (HP), Poder (attack), Voluntad (slows Paranoia
    /// gain). One shared asset (singleton pattern, like InventorySO) - there is
    /// only one player. Vigor/Poder/Voluntad are Inspector-only placeholders for
    /// now: the GDD's permanent hub stat-point allocation UI doesn't exist yet
    /// ([PENDIENTE] - flag any future change here as design-relevant). Health
    /// and Paranoia are runtime state rebuilt in OnEnable, the same way
    /// InventorySO rebuilds its slots - but OnEnable alone isn't enough here
    /// (see ResetRuntimeState).
    /// </summary>
    [CreateAssetMenu(fileName = "New Player Stats", menuName = "Jagara/Player Stats")]
    public class PlayerStatsSO : ScriptableObject
    {
        [Header("Base stats (placeholder until hub allocation exists)")]
        [SerializeField] private int vigor;
        [SerializeField] private int poder;
        [SerializeField] private int voluntad;

        [Header("Paranoia tuning")]
        [SerializeField] private int maxParanoia = 100;
        [Tooltip("Turns needed to gain paranoiaPointsPerInterval, before Voluntad slowdown.")]
        [SerializeField] private int stepsPerParanoiaPoint = 10;
        [Tooltip("Paranoia points gained once stepsPerParanoiaPoint (+ Voluntad slowdown) turns have passed.")]
        [SerializeField] private int paranoiaPointsPerInterval = 1;
        [Tooltip("Extra turns required per point of Voluntad, on top of stepsPerParanoiaPoint - slows gain further.")]
        [SerializeField] private int extraStepsPerVoluntadPoint = 1;

        private int stepsSinceLastParanoiaGain;

        // Accumulates every bump-attack landed on the player within a single
        // player turn (multiple enemies can be adjacent and all attack in the
        // same turn), so PlayerController can post one combined message
        // instead of each attack overwriting the last in the 1-line text box.
        private int turnDamageTaken;
        private int turnAttackerCount;
        private string lastAttackerName;

        public int Poder => poder;
        public int Voluntad => voluntad;

        // The stat -> resource conversion lives here rather than at the call
        // sites, so PlayerController never has to know StatFormulas exists -
        // and so the player stays the only actor running on stats (enemies
        // author their HP/damage directly on EnemyConfigSO).
        public int MaxHP => StatFormulas.ComputeMaxHP(vigor);
        public int AttackDamage => StatFormulas.ComputeAttackDamage(poder);

        public HealthState Health { get; private set; }
        public ParanoiaState Paranoia { get; private set; }

        private void OnEnable()
        {
            ResetRuntimeState();
        }

        /// <summary>
        /// Rebuilds Health/Paranoia from scratch. OnEnable covers a real domain
        /// reload, but this asset survives Play Mode sessions when "Enter Play
        /// Mode Options" has domain reload disabled - OnEnable then does NOT
        /// re-run between sessions, so a stale HP/Paranoia value (sometimes
        /// already 0/dead) would otherwise carry over into a "fresh" run.
        /// NightmareBootstrap.Start() calls this explicitly for that reason,
        /// mirroring messageLog.Clear()/inputGate.ResetGate() there.
        /// </summary>
        public void ResetRuntimeState()
        {
            Health = new HealthState(MaxHP);
            Paranoia = new ParanoiaState(maxParanoia);
            stepsSinceLastParanoiaGain = 0;
            turnDamageTaken = 0;
            turnAttackerCount = 0;
            lastAttackerName = null;
        }

        /// <summary>Records one enemy's bump-attack against the player. Called by EnemyController; the damage itself is already applied to Health by the time this runs.</summary>
        public void RecordIncomingAttack(string attackerName, int damage)
        {
            turnDamageTaken += damage;
            turnAttackerCount++;
            lastAttackerName = attackerName;
        }

        /// <summary>
        /// Returns this turn's accumulated incoming-attack data and clears it for
        /// the next turn. Called once per player turn (see
        /// PlayerController.HandleTurnEnded). Returns false (no message to post)
        /// if nothing attacked the player this turn.
        /// </summary>
        public bool TryConsumeTurnDamage(out int totalDamage, out int attackerCount, out string lastAttacker)
        {
            totalDamage = turnDamageTaken;
            attackerCount = turnAttackerCount;
            lastAttacker = lastAttackerName;

            turnDamageTaken = 0;
            turnAttackerCount = 0;
            lastAttackerName = null;

            return attackerCount > 0;
        }

        /// <summary>
        /// Counts one turn towards the next Paranoia gain, per the GDD's
        /// "Voluntad slows Paranoia gain" rule: every (stepsPerParanoiaPoint +
        /// Voluntad * extraStepsPerVoluntadPoint) turns, Paranoia gains
        /// paranoiaPointsPerInterval points. Called once per resolved player turn.
        /// </summary>
        public void ApplyTurnParanoiaGain()
        {
            stepsSinceLastParanoiaGain++;

            int stepsNeeded = Mathf.Max(1, stepsPerParanoiaPoint + voluntad * extraStepsPerVoluntadPoint);
            if (stepsSinceLastParanoiaGain < stepsNeeded)
            {
                return;
            }

            stepsSinceLastParanoiaGain = 0;
            Paranoia.Gain(paranoiaPointsPerInterval);
        }
    }
}
