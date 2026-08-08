using UnityEngine;

namespace Jagara.Runtime.Gameplay
{
    /// <summary>
    /// Enables/disables the torch flame particle effect as a single isolated
    /// flag. Some levels have no torch; a future level-setup system decides
    /// how to signal that and calls SetTorchActive(false) then. Deliberately
    /// does not touch TorchLight, FlickeringLight2D, or NightmareThemeSO -
    /// this is intentionally scoped small (see docs/superpowers/specs for
    /// the torch-flame-vfx design).
    /// </summary>
    public class TorchFlameToggle : MonoBehaviour
    {
        [SerializeField] private bool torchEnabledByDefault = true;
        [SerializeField] private GameObject torchFlame;

        private void Awake()
        {
            if (torchFlame != null)
            {
                torchFlame.SetActive(torchEnabledByDefault);
            }
            else
            {
                Debug.LogError("TorchFlameToggle: torchFlame reference is not assigned.");
            }
        }

        /// <summary>
        /// Immediately shows/hides the torch flame effect. This is a hard
        /// SetActive, not a graceful fade-out of already-emitted particles.
        /// </summary>
        public void SetTorchActive(bool active)
        {
            if (torchFlame != null)
            {
                torchFlame.SetActive(active);
            }
        }
    }
}
