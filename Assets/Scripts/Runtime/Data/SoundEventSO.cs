using UnityEngine;
using UnityEngine.Audio;

namespace Jagara.Runtime.Data
{
    /// <summary>
    /// Data-only description of a single playable sound: clip variants, mixer
    /// routing, and volume/pitch range. AudioManager owns all playback behavior
    /// (random selection, pooling) - this asset holds no logic, per the project's
    /// SO convention of pure-data configs (see EnemyConfigSO).
    /// </summary>
    [CreateAssetMenu(fileName = "New Sound Event", menuName = "Audio/Sound Event")]
    public class SoundEventSO : ScriptableObject
    {
        [Header("Clips")]
        [Tooltip("TODO: no final SFX/music sourced yet. Empty is safe - AudioManager.Play() " +
                 "logs a warning and no-ops rather than throwing.")]
        [SerializeField] private AudioClip[] clips;

        [Header("Routing")]
        [SerializeField] private AudioMixerGroup outputGroup;

        [Header("Playback")]
        [Range(0f, 1f)]
        [SerializeField] private float volume = 1f;

        [SerializeField] private float pitchMin = 0.95f;
        [SerializeField] private float pitchMax = 1.05f;

        public AudioClip[] Clips => clips;
        public AudioMixerGroup OutputGroup => outputGroup;
        public float Volume => volume;
        public float PitchMin => pitchMin;
        public float PitchMax => pitchMax;

        private void OnValidate()
        {
            volume = Mathf.Clamp01(volume);
            pitchMin = Mathf.Max(0.01f, pitchMin);
            pitchMax = Mathf.Max(pitchMin, pitchMax);
        }
    }
}
