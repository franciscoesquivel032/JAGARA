using UnityEngine;

namespace Jagara.Runtime.Gameplay
{
    /// <summary>
    /// Pure math for a decaying camera-shake offset, decoupled from
    /// MonoBehaviour so it can run in Edit Mode tests. See CameraShake for
    /// the runtime wrapper. Mirrors PlayerMotionAnimator.ComputeHitShakeOffset's
    /// decaying-sine style, but two-axis: X and Y use different frequencies
    /// plus a phase offset on Y, so the traced path is a Lissajous-ish curve
    /// rather than a straight diagonal line.
    /// </summary>
    public static class CameraShakeMath
    {
        private const float FrequencyX = 5f;
        private const float FrequencyY = 7f;
        private const float PhaseY = Mathf.PI / 3f;

        // Decaying two-axis shake: amplitude decays linearly to 0 across
        // progress [0, 1], same envelope shape as PlayerMotionAnimator's hit
        // shake.
        public static Vector2 ComputeOffset(float progress, float magnitude)
        {
            float p = Mathf.Clamp01(progress);
            float decay = 1f - p;

            float x = magnitude * Mathf.Sin(p * FrequencyX * Mathf.PI * 2f) * decay;
            float y = magnitude * Mathf.Sin(p * FrequencyY * Mathf.PI * 2f + PhaseY) * decay;
            return new Vector2(x, y);
        }
    }
}
