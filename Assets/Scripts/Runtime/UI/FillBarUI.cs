using UnityEngine;
using UnityEngine.UI;

namespace Jagara.Runtime.UI
{
    /// <summary>
    /// Purely visual fill bar (e.g. HP, Paranoia): no knowledge of what resource
    /// it represents. Binders (HealthBarBinder, ParanoiaBarBinder) call SetValue
    /// whenever their underlying resource changes.
    /// </summary>
    public class FillBarUI : MonoBehaviour
    {
        [SerializeField] private Image fillImage;

        public void SetValue(int current, int max)
        {
            fillImage.fillAmount = max > 0 ? (float)current / max : 0f;
        }
    }
}
