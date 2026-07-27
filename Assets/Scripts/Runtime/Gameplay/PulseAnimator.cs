using UnityEngine;

namespace Jagara.Runtime.Gameplay
{
    /// <summary>
    /// Pure sine-wave "breathing" scale math, decoupled from MonoBehaviour so it
    /// can run in Edit Mode tests. See PulsingHighlight for the runtime wrapper.
    /// </summary>
    public static class PulseAnimator
    {
        public static float ComputeScale(float time, float baseScale, float amplitude, float speed, float phaseOffset)
        {
            return baseScale + Mathf.Sin(time * speed + phaseOffset) * amplitude;
        }
    }
}
