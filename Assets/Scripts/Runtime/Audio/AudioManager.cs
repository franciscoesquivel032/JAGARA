using UnityEngine;
using UnityEngine.Audio;
using Jagara.Runtime.Data;

namespace Jagara.Runtime.Audio
{
    /// <summary>
    /// Pooled, mixer-aware playback for SoundEventSO assets. A fixed pool of
    /// AudioSources is built once (normally in Awake, or lazily on first Play()
    /// if Awake hasn't run yet) so Play() never Instantiates per call.
    ///
    /// TODO(ducking): Mixer ducking / Send-Receive routing (e.g. Combat and
    /// VoiceOver lines should duck Music) is intentionally NOT wired here - it's
    /// a manual step in the Audio Mixer window (add a Duck Volume effect on the
    /// Music group's chain, sidechained from a Send on Combat/VoiceOver). The
    /// relevant groups live in Assets/Audio/JagaraMixer.mixer at
    /// Master/Music, Master/SFX/Combat, and Master/VoiceOver - route ducking
    /// there, not in code.
    /// </summary>
    [DisallowMultipleComponent]
    public class AudioManager : MonoBehaviour
    {
        private const int PoolSize = 16;

        private AudioSource[] pool;
        private int nextIndex;

        /// <summary>Read-only pool size, for tests/debugging - not part of the spec'd API surface.</summary>
        public int PooledSourceCount => pool?.Length ?? 0;

        private void Awake()
        {
            EnsurePoolBuilt();
        }

        // Guards against Play() being called before this component's own Awake
        // has run (script execution order hazards, or - as observed under the
        // Unity Test Framework - AddComponent not synchronously invoking Awake
        // in every Edit Mode context). Idempotent: a no-op once the pool exists.
        private void EnsurePoolBuilt()
        {
            if (pool != null)
                return;

            BuildPool();
        }

        private void BuildPool()
        {
            pool = new AudioSource[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                var go = new GameObject($"PooledSource_{i}");
                go.transform.SetParent(transform);
                var source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                pool[i] = source;
            }
        }

        public void Play(SoundEventSO soundEvent)
        {
            EnsurePoolBuilt();

            if (soundEvent == null)
            {
                Debug.LogError("AudioManager.Play called with a null SoundEventSO.");
                return;
            }

            if (soundEvent.Clips == null || soundEvent.Clips.Length == 0)
            {
                Debug.LogWarning($"SoundEventSO '{soundEvent.name}' has no clips assigned - skipping playback.");
                return;
            }

            AudioSource source = GetAvailableSource();
            source.clip = soundEvent.Clips[Random.Range(0, soundEvent.Clips.Length)];
            source.outputAudioMixerGroup = soundEvent.OutputGroup;
            source.volume = soundEvent.Volume;
            source.pitch = Random.Range(soundEvent.PitchMin, soundEvent.PitchMax);
            source.Play();
        }

        private AudioSource GetAvailableSource()
        {
            // Plain loop, no LINQ. Search starting at the round-robin cursor so
            // repeated steals don't always cut off the same slot.
            for (int i = 0; i < pool.Length; i++)
            {
                int idx = (nextIndex + i) % pool.Length;
                if (!pool[idx].isPlaying)
                {
                    nextIndex = (idx + 1) % pool.Length;
                    return pool[idx];
                }
            }

            // Every source busy: steal the cursor slot rather than growing the
            // pool or throwing. Pool size is a hard ceiling by design.
            AudioSource stolen = pool[nextIndex];
            nextIndex = (nextIndex + 1) % pool.Length;
            return stolen;
        }

        public void TransitionToSnapshot(AudioMixerSnapshot snapshot, float duration)
        {
            if (snapshot == null)
            {
                Debug.LogError("AudioManager.TransitionToSnapshot called with a null snapshot.");
                return;
            }

            snapshot.TransitionTo(duration);
        }
    }
}
