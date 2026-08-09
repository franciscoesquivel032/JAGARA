using NUnit.Framework;
using Jagara.Runtime.Audio;

namespace Jagara.Tests.EditMode
{
    /// <summary>
    /// LinearToDecibel is the only math standing between a UI slider and the
    /// mixer's dB scale - a wrong floor or a NaN/-Infinity here would silently
    /// break volume sliders or crash AudioMixer.SetFloat at 0.
    /// </summary>
    public class AudioVolumeControllerTests
    {
        [Test]
        public void LinearToDecibel_FullVolume_IsUnityGain()
        {
            Assert.AreEqual(0f, AudioVolumeController.LinearToDecibel(1f));
        }

        [Test]
        public void LinearToDecibel_Zero_ReturnsFloorNotNaNOrInfinity()
        {
            float result = AudioVolumeController.LinearToDecibel(0f);
            Assert.AreEqual(-80f, result);
            Assert.IsFalse(float.IsNaN(result));
            Assert.IsFalse(float.IsInfinity(result));
        }

        [Test]
        public void LinearToDecibel_IsMonotonicallyIncreasing()
        {
            float low = AudioVolumeController.LinearToDecibel(0.1f);
            float mid = AudioVolumeController.LinearToDecibel(0.5f);
            float high = AudioVolumeController.LinearToDecibel(0.9f);

            Assert.Less(low, mid);
            Assert.Less(mid, high);
        }
    }
}
