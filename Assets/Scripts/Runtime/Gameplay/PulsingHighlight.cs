using UnityEngine;

namespace Jagara.Runtime.Gameplay
{
    /// <summary>
    /// Drives a continuous breathing scale pulse (see PulseAnimator) on this
    /// transform, with a randomized phase offset so multiple instances don't
    /// pulse in sync.
    /// </summary>
    public class PulsingHighlight : MonoBehaviour
    {
        [SerializeField] private float baseScale = 1f;
        [SerializeField] private float amplitude = 0.08f;
        [SerializeField] private float speed = 3f;

        private float phaseOffset;

        private void Awake()
        {
            phaseOffset = Random.value * Mathf.PI * 2f;
        }

        private void Update()
        {
            float scale = PulseAnimator.ComputeScale(Time.time, baseScale, amplitude, speed, phaseOffset);
            transform.localScale = Vector3.one * scale;
        }
    }
}
