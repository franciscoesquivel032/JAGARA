using Jagara.Runtime.Resources;
using UnityEngine;

namespace Jagara.Runtime.UI
{
    /// <summary>
    /// Wires a FillBarUI to a specific entity's HealthState. Kept separate from
    /// HealthState itself since HealthState is plain C# with no UI knowledge -
    /// this is the glue, used by both PlayerController and EnemyController.
    /// </summary>
    public class HealthBarBinder : MonoBehaviour
    {
        [SerializeField] private FillBarUI bar;

        private HealthState health;

        public void Bind(HealthState health)
        {
            Unbind();

            this.health = health;
            if (this.health == null)
            {
                return;
            }

            this.health.OnHPChanged += HandleHPChanged;
            bar.SetValue(this.health.Current, this.health.Max);
        }

        public void Unbind()
        {
            if (health == null)
            {
                return;
            }

            health.OnHPChanged -= HandleHPChanged;
            health = null;
        }

        private void HandleHPChanged(int current, int max)
        {
            bar.SetValue(current, max);
        }
    }
}
