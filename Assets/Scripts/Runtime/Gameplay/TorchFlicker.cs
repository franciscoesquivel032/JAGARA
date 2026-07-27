using UnityEngine;

namespace Jagara.Runtime.Gameplay
{
    /// <summary>
    /// Pure Perlin-noise flicker math, decoupled from MonoBehaviour/Light2D so it
    /// can run in Edit Mode tests. See FlickeringLight2D for the runtime wrapper.
    /// </summary>
    public static class TorchFlicker
    {
        public static float ComputeValue(float time, float seed, float speed, float baseValue, float amplitude)
        {
            float noise = Mathf.PerlinNoise(time * speed + seed, 0f);
            return baseValue + (noise * 2f - 1f) * amplitude;
        }
    }
}
