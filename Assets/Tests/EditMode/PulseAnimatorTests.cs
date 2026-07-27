using NUnit.Framework;
using Jagara.Runtime.Gameplay;

namespace Jagara.Tests.EditMode
{
    public class PulseAnimatorTests
    {
        [TestCase(0f)]
        [TestCase(1.37f)]
        [TestCase(52.9f)]
        [TestCase(1000f)]
        public void ComputeScale_StaysWithinAmplitudeBounds(float time)
        {
            const float baseScale = 1f;
            const float amplitude = 0.08f;

            float value = PulseAnimator.ComputeScale(time, baseScale, amplitude, speed: 3f, phaseOffset: 0.6f);

            Assert.GreaterOrEqual(value, baseScale - amplitude);
            Assert.LessOrEqual(value, baseScale + amplitude);
        }

        [Test]
        public void ComputeScale_IsDeterministic_ForSameInputs()
        {
            float a = PulseAnimator.ComputeScale(12.34f, 1f, 0.08f, 3f, 0.6f);
            float b = PulseAnimator.ComputeScale(12.34f, 1f, 0.08f, 3f, 0.6f);

            Assert.AreEqual(a, b);
        }

        [Test]
        public void ComputeScale_DifferentPhaseOffsets_ProduceDifferentValues()
        {
            float valueA = PulseAnimator.ComputeScale(10.37f, 1f, 0.08f, 3f, phaseOffset: 0.1f);
            float valueB = PulseAnimator.ComputeScale(10.37f, 1f, 0.08f, 3f, phaseOffset: 2.5f);

            Assert.AreNotEqual(valueA, valueB);
        }
    }
}
