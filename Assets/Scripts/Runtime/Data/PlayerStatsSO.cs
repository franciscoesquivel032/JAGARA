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
    /// InventorySO rebuilds its slots.
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
        [SerializeField] private int baseParanoiaGainPerTurn = 3;
        [SerializeField] private int paranoiaReductionPerVoluntadPoint = 1;
        [SerializeField] private int minParanoiaGainPerTurn = 1;

        public int Poder => poder;
        public int Voluntad => voluntad;

        public HealthState Health { get; private set; }
        public ParanoiaState Paranoia { get; private set; }

        private void OnEnable()
        {
            Health = new HealthState(StatFormulas.ComputeMaxHP(vigor));
            Paranoia = new ParanoiaState(maxParanoia);
        }

        /// <summary>
        /// Applies one turn's worth of Paranoia gain, per the GDD's "Voluntad
        /// slows Paranoia gain" rule: gain = max(minGain, baseGain - Voluntad
        /// * reductionPerPoint). Called once per resolved player turn.
        /// </summary>
        public void ApplyTurnParanoiaGain()
        {
            int gain = Mathf.Max(minParanoiaGainPerTurn, baseParanoiaGainPerTurn - voluntad * paranoiaReductionPerVoluntadPoint);
            Paranoia.Gain(gain);
        }
    }
}
