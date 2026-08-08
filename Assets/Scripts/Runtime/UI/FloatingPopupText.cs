using System.Collections;
using TMPro;
using UnityEngine;

namespace Jagara.Runtime.UI
{
    /// <summary>
    /// Animates a single spawned popup: pops in with a scale tween, drifts
    /// upward along a randomized direction (for visual variety when several
    /// spawn close together), then fades out and destroys itself.
    /// Fire-and-forget - PopupSpawner sets the text/colour once via Play()
    /// and never touches this GameObject again. No Canvas needed: the camera
    /// is orthographic 2D, so a 3D TextMeshPro renders face-on directly in
    /// world space, sorted above sprites via its own sorting layer/order.
    /// </summary>
    [RequireComponent(typeof(TextMeshPro))]
    public class FloatingPopupText : MonoBehaviour
    {
        [SerializeField] private TextMeshPro label;

        [Header("Motion")]
        [SerializeField] private float duration = 0.9f;
        [SerializeField] private float riseDistance = 0.75f;
        [Tooltip("Random drift cone around straight up, in degrees each way.")]
        [SerializeField] private float randomAngleRangeDegrees = 25f;

        [Header("Scale")]
        [Tooltip("Scale at the very start of the pop-in.")]
        [SerializeField] private float startScale = 0.1f;
        [Tooltip("Peak scale the pop-in overshoots to before settling at 1 - the bigger this is over 1, the more exaggerated the punch.")]
        [SerializeField] private float overshootScale = 1.4f;
        [Tooltip("Time to grow from startScale to overshootScale.")]
        [SerializeField] private float popInDuration = 0.15f;
        [Tooltip("Time to settle from overshootScale back down to 1 after the pop-in.")]
        [SerializeField] private float settleDuration = 0.12f;

        [Header("Fade")]
        [Tooltip("Fraction of the total duration at which the fade-out begins.")]
        [Range(0f, 0.95f)]
        [SerializeField] private float fadeStartFraction = 0.6f;

        private void Awake()
        {
            if (label == null)
            {
                label = GetComponent<TextMeshPro>();
            }
        }

        /// <summary>Sets this popup's text/colour and starts its animation.</summary>
        public void Play(string text, Color color)
        {
            label.text = text;
            label.color = color;
            StartCoroutine(Animate());
        }

        private IEnumerator Animate()
        {
            Vector3 origin = transform.position;

            float angleDegrees = 90f + Random.Range(-randomAngleRangeDegrees, randomAngleRangeDegrees);
            Vector3 direction = new Vector3(Mathf.Cos(angleDegrees * Mathf.Deg2Rad), Mathf.Sin(angleDegrees * Mathf.Deg2Rad), 0f);
            Vector3 destination = origin + direction * riseDistance;

            Color baseColor = label.color;
            transform.localScale = Vector3.one * startScale;

            float settleEnd = popInDuration + settleDuration;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                transform.position = Vector3.Lerp(origin, destination, t);

                float scale;
                if (elapsed <= popInDuration)
                {
                    // Punch past 1 first - a plain 0->1 ease reads as timid;
                    // overshooting and settling back reads as an actual hit.
                    float popT = popInDuration > 0f ? Mathf.Clamp01(elapsed / popInDuration) : 1f;
                    scale = Mathf.Lerp(startScale, overshootScale, EaseOut(popT));
                }
                else if (elapsed <= settleEnd)
                {
                    float settleT = settleDuration > 0f ? Mathf.Clamp01((elapsed - popInDuration) / settleDuration) : 1f;
                    scale = Mathf.Lerp(overshootScale, 1f, EaseOut(settleT));
                }
                else
                {
                    scale = 1f;
                }
                transform.localScale = Vector3.one * scale;

                float fadeRange = 1f - fadeStartFraction;
                float fadeT = t > fadeStartFraction ? (t - fadeStartFraction) / fadeRange : 0f;
                Color c = baseColor;
                c.a = baseColor.a * (1f - Mathf.Clamp01(fadeT));
                label.color = c;

                yield return null;
            }

            Destroy(gameObject);
        }

        private static float EaseOut(float t) => 1f - (1f - t) * (1f - t);
    }
}
