using Jagara.Runtime.Resources;
using TMPro;
using UnityEngine;

namespace Jagara.Runtime.UI
{
    /// <summary>
    /// Persistent single-line HUD readout above the text box: floor, level, HP
    /// and Paranoia. Floor/level have no backing systems yet ([PENDIENTE] per
    /// the GDD - no floor-progression or character-level design exists), so
    /// they are fixed Inspector placeholders until those systems land.
    /// </summary>
    public class PlayerStatusHudController : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;

        [Tooltip("Placeholder - no floor-progression system exists yet.")]
        [SerializeField] private int floorPlaceholder = 1;

        [Tooltip("Placeholder - no character-level system exists yet.")]
        [SerializeField] private int levelPlaceholder = 1;

        private HealthState health;
        private ParanoiaState paranoia;

        public void Bind(HealthState health, ParanoiaState paranoia)
        {
            Unbind();

            this.health = health;
            this.paranoia = paranoia;

            if (this.health != null)
            {
                this.health.OnHPChanged += HandleStatChanged;
            }

            if (this.paranoia != null)
            {
                this.paranoia.OnParanoiaChanged += HandleStatChanged;
            }

            Render();
        }

        public void Unbind()
        {
            if (health != null)
            {
                health.OnHPChanged -= HandleStatChanged;
                health = null;
            }

            if (paranoia != null)
            {
                paranoia.OnParanoiaChanged -= HandleStatChanged;
                paranoia = null;
            }
        }

        private void OnDisable()
        {
            Unbind();
        }

        private void HandleStatChanged(int current, int max)
        {
            Render();
        }

        private void Render()
        {
            if (label == null || health == null || paranoia == null)
            {
                return;
            }

            int paranoiaPercent = paranoia.Max > 0 ? Mathf.RoundToInt(paranoia.Current / (float)paranoia.Max * 100f) : 0;
            label.text = $"Floor - {floorPlaceholder}      Lv. - {levelPlaceholder}      HP - {health.Current} / {health.Max}     Par - {paranoiaPercent}%";
        }
    }
}
