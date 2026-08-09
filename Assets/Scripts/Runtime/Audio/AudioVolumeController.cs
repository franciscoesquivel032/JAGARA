using UnityEngine;
using UnityEngine.Audio;

namespace Jagara.Runtime.Audio
{
    /// <summary>
    /// Converts UI-facing linear [0,1] volume sliders to the mixer's decibel
    /// scale and persists them via PlayerPrefs. LinearToDecibel is static/pure
    /// so it's testable without a mixer or scene.
    /// </summary>
    public class AudioVolumeController : MonoBehaviour
    {
        private const float MinDecibel = -80f;
        private const float SilenceThreshold = 0.0001f;

        private const string MasterKey = "MasterVolume";
        private const string MusicKey = "MusicVolume";
        private const string SFXKey = "SFXVolume";
        private const string AmbienceKey = "AmbienceVolume";
        private const string VoiceOverKey = "VoiceOverVolume";

        [SerializeField] private AudioMixer mixer;

        /// <summary>
        /// Converts a linear [0,1] volume to the decibel scale AudioMixer expects.
        /// Floors explicitly at -80dB instead of evaluating Log10 near zero, so
        /// this never returns NaN or -Infinity.
        /// </summary>
        public static float LinearToDecibel(float linear01)
        {
            if (linear01 <= SilenceThreshold)
                return MinDecibel;

            return Mathf.Clamp(20f * Mathf.Log10(linear01), MinDecibel, 0f);
        }

        public void SetMasterVolume(float linear01) => SetVolume(MasterKey, linear01);
        public void SetMusicVolume(float linear01) => SetVolume(MusicKey, linear01);
        public void SetSFXVolume(float linear01) => SetVolume(SFXKey, linear01);
        public void SetAmbienceVolume(float linear01) => SetVolume(AmbienceKey, linear01);
        public void SetVoiceOverVolume(float linear01) => SetVolume(VoiceOverKey, linear01);

        private void SetVolume(string exposedParam, float linear01)
        {
            if (mixer == null)
            {
                Debug.LogError($"AudioVolumeController has no mixer assigned - cannot set {exposedParam}.");
                return;
            }

            mixer.SetFloat(exposedParam, LinearToDecibel(linear01));
            PlayerPrefs.SetFloat(exposedParam, linear01);
        }

        /// <summary>Call once at startup to re-apply persisted volumes; defaults to full volume if unset.</summary>
        public void LoadSavedVolumes()
        {
            SetMasterVolume(PlayerPrefs.GetFloat(MasterKey, 1f));
            SetMusicVolume(PlayerPrefs.GetFloat(MusicKey, 1f));
            SetSFXVolume(PlayerPrefs.GetFloat(SFXKey, 1f));
            SetAmbienceVolume(PlayerPrefs.GetFloat(AmbienceKey, 1f));
            SetVoiceOverVolume(PlayerPrefs.GetFloat(VoiceOverKey, 1f));
        }
    }
}
