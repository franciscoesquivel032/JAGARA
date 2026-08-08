using Jagara.Runtime.Events;
using Jagara.Runtime.Resources;
using UnityEngine;

namespace Jagara.Runtime.UI
{
    /// <summary>
    /// Turns HealthState.OnHPChanged into a PopupRequest: a one-shot spawned
    /// effect rather than a persistently bound bar. Mirrors PlayerController/
    /// EnemyController's Bind/Unbind wiring shape. Computes the HP delta itself
    /// (rather than depending on a specific combat call site) so it reacts to any HP change - damage,
    /// heal, a future damage-over-time tick - with no changes to
    /// CombatResolver or the PerformAttack methods.
    /// </summary>
    public class HpPopupBinder : MonoBehaviour
    {
        [SerializeField] private PopupRequestEventSO popupRequestEvent;
        [SerializeField] private Transform spawnAnchor;
        [SerializeField] private Vector3 spawnOffset = new Vector3(0f, 0.6f, 0f);

        [Tooltip("Colour for a losing-HP popup on this entity specifically - white on the enemy prefab (the player's own damage), red on the player prefab (an enemy's damage). Healing is always HpPopupFormatter.HealColor regardless of this value.")]
        [SerializeField] private Color damageColor = Color.white;

        private HealthState health;
        private int lastKnownHP;

        private void Awake()
        {
            if (spawnAnchor == null)
            {
                spawnAnchor = transform;
            }
        }

        public void Bind(HealthState health)
        {
            Unbind();

            if (popupRequestEvent == null)
            {
                Debug.LogError($"HpPopupBinder on {name}: popupRequestEvent reference is not assigned; no HP popups will be shown.");
                return;
            }

            this.health = health;
            if (this.health == null)
            {
                return;
            }

            lastKnownHP = this.health.Current;
            this.health.OnHPChanged += HandleHPChanged;
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

        private void OnDisable() => Unbind();

        private void HandleHPChanged(int current, int max)
        {
            int delta = current - lastKnownHP;
            lastKnownHP = current;

            var formatted = HpPopupFormatter.Format(delta, damageColor);
            if (formatted == null)
            {
                return;
            }

            var request = new PopupRequest(spawnAnchor.position + spawnOffset, formatted.Value.Text, formatted.Value.Color);
            popupRequestEvent.Raise(request);
        }
    }
}
