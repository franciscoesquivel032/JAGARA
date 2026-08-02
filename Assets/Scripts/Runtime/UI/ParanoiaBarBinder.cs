using Jagara.Runtime.Resources;
using UnityEngine;

namespace Jagara.Runtime.UI
{
    /// <summary>
    /// Wires a FillBarUI to the player's ParanoiaState. Player-only, mirroring
    /// HealthBarBinder's pattern - Paranoia has no enemy equivalent per the GDD.
    /// </summary>
    public class ParanoiaBarBinder : MonoBehaviour
    {
        [SerializeField] private FillBarUI bar;

        private ParanoiaState paranoia;

        public void Bind(ParanoiaState paranoia)
        {
            Unbind();

            this.paranoia = paranoia;
            if (this.paranoia == null)
            {
                return;
            }

            this.paranoia.OnParanoiaChanged += HandleParanoiaChanged;
            bar.SetValue(this.paranoia.Current, this.paranoia.Max);
        }

        public void Unbind()
        {
            if (paranoia == null)
            {
                return;
            }

            paranoia.OnParanoiaChanged -= HandleParanoiaChanged;
            paranoia = null;
        }

        private void HandleParanoiaChanged(int current, int max)
        {
            bar.SetValue(current, max);
        }
    }
}
