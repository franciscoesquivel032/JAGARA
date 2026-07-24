using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Jagara.Runtime.Gameplay
{
    /// <summary>
    /// Drives a Light2D's intensity and outer radius with independent Perlin-noise
    /// flicker (see TorchFlicker), so multiple instances don't pulse in sync.
    /// </summary>
    [RequireComponent(typeof(Light2D))]
    public class FlickeringLight2D : MonoBehaviour
    {
        [SerializeField] private Light2D targetLight;
        [SerializeField] private float baseIntensity = 1.2f;
        [SerializeField] private float intensityAmplitude = 0.3f;
        [SerializeField] private float baseOuterRadius = 3.5f;
        [SerializeField] private float radiusAmplitude = 0.35f;
        [SerializeField] private float noiseSpeed = 1.5f;

        private float intensitySeed;
        private float radiusSeed;

        private void Awake()
        {
            if (targetLight == null)
            {
                targetLight = GetComponent<Light2D>();
            }

            if (targetLight == null)
            {
                Debug.LogError("FlickeringLight2D: no Light2D found on this GameObject.");
                enabled = false;
                return;
            }

            intensitySeed = Random.value * 1000f;
            radiusSeed = Random.value * 1000f;
        }

        private void Update()
        {
            targetLight.intensity = TorchFlicker.ComputeValue(
                Time.time, intensitySeed, noiseSpeed, baseIntensity, intensityAmplitude);

            targetLight.pointLightOuterRadius = TorchFlicker.ComputeValue(
                Time.time, radiusSeed, noiseSpeed * 0.7f, baseOuterRadius, radiusAmplitude);
        }
    }
}
