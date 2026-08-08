using NUnit.Framework;
using UnityEngine;
using Jagara.Runtime.Gameplay;

namespace Jagara.Tests.EditMode
{
    public class CameraShakeMathTests
    {
        [Test]
        public void ComputeOffset_IsDeterministic_ForSameInputs()
        {
            Vector2 a = CameraShakeMath.ComputeOffset(0.4f, 0.12f);
            Vector2 b = CameraShakeMath.ComputeOffset(0.4f, 0.12f);

            Assert.AreEqual(a, b);
        }

        [Test]
        public void ComputeOffset_AmplitudeDecaysAsProgressAdvances()
        {
            // At p = 0.1 and p = 0.9, FrequencyX * p (5p) is a half-integer
            // (0.5 and 4.5), so the X term is exactly zero at both samples -
            // only the Y term and the shared (1 - progress) decay envelope
            // differ between them, isolating the decay check.
            const float magnitude = 0.12f;
            Vector2 early = CameraShakeMath.ComputeOffset(0.1f, magnitude);
            Vector2 late = CameraShakeMath.ComputeOffset(0.9f, magnitude);

            Assert.AreEqual(0f, early.x, 1e-4f);
            Assert.AreEqual(0f, late.x, 1e-4f);
            Assert.Greater(early.magnitude, late.magnitude);
        }

        [Test]
        public void ComputeOffset_IsZero_AtFullProgress()
        {
            // decay = (1 - progress) hits exactly 0 at progress = 1 regardless
            // of the sine terms, so the offset must vanish on both axes.
            Vector2 offset = CameraShakeMath.ComputeOffset(1f, 0.12f);

            Assert.AreEqual(0f, offset.x, 1e-5f);
            Assert.AreEqual(0f, offset.y, 1e-5f);
        }

        [TestCase(-0.5f)]
        [TestCase(1.5f)]
        public void ComputeOffset_ClampsOutOfRangeProgress(float progress)
        {
            Vector2 clamped = progress < 0f
                ? CameraShakeMath.ComputeOffset(0f, 0.12f)
                : CameraShakeMath.ComputeOffset(1f, 0.12f);

            Vector2 actual = CameraShakeMath.ComputeOffset(progress, 0.12f);

            Assert.AreEqual(clamped, actual);
        }

        [Test]
        public void ComputeOffset_ScalesWithMagnitude()
        {
            Vector2 small = CameraShakeMath.ComputeOffset(0.1f, 0.05f);
            Vector2 large = CameraShakeMath.ComputeOffset(0.1f, 0.5f);

            Assert.Greater(large.magnitude, small.magnitude);
        }

        [Test]
        public void ComputeOffset_XAndYAreNotInPhase()
        {
            // Different frequencies + a phase offset on Y means the two axes
            // shouldn't trace a straight diagonal line - i.e. x and y aren't
            // simple scalar multiples of one another across progress.
            Vector2 a = CameraShakeMath.ComputeOffset(0.2f, 0.12f);
            Vector2 b = CameraShakeMath.ComputeOffset(0.35f, 0.12f);

            float ratioA = a.x / a.y;
            float ratioB = b.x / b.y;

            Assert.Greater(Mathf.Abs(ratioA - ratioB), 1e-3f);
        }
    }
}
