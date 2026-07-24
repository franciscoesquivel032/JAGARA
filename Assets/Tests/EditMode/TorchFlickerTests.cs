using NUnit.Framework;
using Jagara.Runtime.Gameplay;

namespace Jagara.Tests.EditMode
{
    public class TorchFlickerTests
    {
        [TestCase(0f)]
        [TestCase(1.37f)]
        [TestCase(52.9f)]
        [TestCase(1000f)]
        public void ComputeValue_StaysWithinAmplitudeBounds(float time)
        {
            const float baseValue = 1.2f;
            const float amplitude = 0.3f;

            float value = TorchFlicker.ComputeValue(time, seed: 42f, speed: 1.5f, baseValue, amplitude);

            Assert.GreaterOrEqual(value, baseValue - amplitude);
            Assert.LessOrEqual(value, baseValue + amplitude);
        }

        [Test]
        public void ComputeValue_IsDeterministic_ForSameInputs()
        {
            float a = TorchFlicker.ComputeValue(12.34f, 7f, 1.5f, 1.2f, 0.3f);
            float b = TorchFlicker.ComputeValue(12.34f, 7f, 1.5f, 1.2f, 0.3f);

            Assert.AreEqual(a, b);
        }

        [Test]
        public void ComputeValue_DifferentSeeds_ProduceDifferentValues()
        {
            // Non-integer time/seed values deliberately avoid Perlin lattice points
            // (integer x with y=0 degenerates to the same value regardless of seed).
            float valueA = TorchFlicker.ComputeValue(10.37f, 1.23f, 1.5f, 1.2f, 0.3f);
            float valueB = TorchFlicker.ComputeValue(10.37f, 500.87f, 1.5f, 1.2f, 0.3f);

            Assert.AreNotEqual(valueA, valueB);
        }
    }
}
