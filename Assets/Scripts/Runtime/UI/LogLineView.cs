using TMPro;
using UnityEngine;

namespace Jagara.Runtime.UI
{
    /// <summary>
    /// One line slot of the action log. Exists only so TextBoxController can hold a
    /// single array of slots instead of two parallel arrays of TMP_Text and
    /// CanvasGroup that could drift out of step in the Inspector.
    /// <para>
    /// Opacity is per-line rather than per-box because the log already embeds
    /// rich-text colour tags for item and enemy names, and a TMP colour tag resets
    /// the alpha channel - so a &lt;alpha&gt; tag around a whole line would leave
    /// those names at full brightness. A CanvasGroup fades the rendered mesh and
    /// sidesteps the tag interaction entirely.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class LogLineView : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private CanvasGroup canvasGroup;

        public float Alpha
        {
            get => canvasGroup != null ? canvasGroup.alpha : 0f;
            set
            {
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = value;
                }
            }
        }

        /// <summary>The text currently in the slot; empty when the slot is unused.</summary>
        public string Text => label != null ? label.text : string.Empty;

        private void Awake()
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }

            if (label == null)
            {
                Debug.LogError($"LogLineView on {name}: label reference is not assigned; this log line will always be blank.");
            }
        }

        public void SetText(string text)
        {
            if (label != null)
            {
                label.text = text ?? string.Empty;
            }
        }

        public void Clear()
        {
            SetText(string.Empty);
            Alpha = 0f;
        }
    }
}
