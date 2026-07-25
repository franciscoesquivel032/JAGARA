using NUnit.Framework;
using UnityEngine;
using Jagara.Runtime.Gameplay;

namespace Jagara.Tests.EditMode
{
    public class PlayerMotionAnimatorTests
    {
        [TestCase(0f)]
        [TestCase(1.37f)]
        [TestCase(52.9f)]
        [TestCase(1000f)]
        public void ComputeIdleBreathScale_NeverStretchesAboveOne_OnY(float time)
        {
            Vector2 scale = PlayerMotionAnimator.ComputeIdleBreathScale(time, amount: 0.03125f, speed: 2.5f);

            Assert.LessOrEqual(scale.y, 1f + 1e-5f);
            Assert.GreaterOrEqual(scale.y, 1f - 0.03125f - 1e-5f);
        }

        [Test]
        public void ComputeIdleBreathScale_WidensAsItSquashes()
        {
            const float speed = 2.5f;
            float fullSquashTime = 3f * Mathf.PI / (2f * speed);

            Vector2 scale = PlayerMotionAnimator.ComputeIdleBreathScale(fullSquashTime, 0.03125f, speed);

            Assert.AreEqual(1f - 0.03125f, scale.y, 1e-4f);
            Assert.AreEqual(1f + 0.03125f, scale.x, 1e-4f);
        }

        [Test]
        public void ComputeIdleBreathScale_OscillatesOverTime()
        {
            Vector2 a = PlayerMotionAnimator.ComputeIdleBreathScale(0f, 0.03125f, 2.5f);
            Vector2 b = PlayerMotionAnimator.ComputeIdleBreathScale(0.6f, 0.03125f, 2.5f);

            Assert.AreNotEqual(a.y, b.y);
        }

        [Test]
        public void ComputeIdleBreathScale_IsDeterministic_ForSameInputs()
        {
            Vector2 a = PlayerMotionAnimator.ComputeIdleBreathScale(12.34f, 0.03125f, 2.5f);
            Vector2 b = PlayerMotionAnimator.ComputeIdleBreathScale(12.34f, 0.03125f, 2.5f);

            Assert.AreEqual(a, b);
        }

        [Test]
        public void ComputeGroundedOffset_LowersSprite_WhenSquashed_SoFeetStayPlanted()
        {
            float offset = PlayerMotionAnimator.ComputeGroundedOffset(scaleY: 0.9f, spriteHeight: 1f);

            Assert.AreEqual(-0.05f, offset, 1e-5f);
        }

        [Test]
        public void ComputeGroundedOffset_IsZero_AtIdentityScale()
        {
            float offset = PlayerMotionAnimator.ComputeGroundedOffset(scaleY: 1f, spriteHeight: 1f);

            Assert.AreEqual(0f, offset, 1e-5f);
        }

        [TestCase(0f)]
        [TestCase(1f)]
        public void ComputeHopHeight_IsZero_AtStartAndEnd(float progress)
        {
            float value = PlayerMotionAnimator.ComputeHopHeight(progress, hopHeight: 0.125f);

            Assert.AreEqual(0f, value, 1e-5f);
        }

        [Test]
        public void ComputeHopHeight_PeaksAtMidpoint()
        {
            const float hopHeight = 0.125f;

            float apex = PlayerMotionAnimator.ComputeHopHeight(0.5f, hopHeight);
            float rising = PlayerMotionAnimator.ComputeHopHeight(0.25f, hopHeight);
            float falling = PlayerMotionAnimator.ComputeHopHeight(0.75f, hopHeight);

            Assert.AreEqual(hopHeight, apex, 1e-5f);
            Assert.Greater(apex, rising);
            Assert.Greater(apex, falling);
        }

        [TestCase(-0.5f)]
        [TestCase(1.5f)]
        public void ComputeHopHeight_ClampsOutOfRangeProgress(float progress)
        {
            float value = PlayerMotionAnimator.ComputeHopHeight(progress, hopHeight: 0.125f);

            Assert.AreEqual(0f, value, 1e-5f);
        }

        [TestCase(0f)]
        [TestCase(1f)]
        public void ComputeHopScale_IsIdentity_AtStartAndEnd(float progress)
        {
            Vector2 scale = PlayerMotionAnimator.ComputeHopScale(progress, squashAmount: 0.06f);

            Assert.AreEqual(1f, scale.x, 1e-5f);
            Assert.AreEqual(1f, scale.y, 1e-5f);
        }

        [Test]
        public void ComputeHopScale_StretchesVerticallyAtApex()
        {
            Vector2 scale = PlayerMotionAnimator.ComputeHopScale(0.5f, squashAmount: 0.06f);

            Assert.Greater(scale.y, 1f);
            Assert.Less(scale.x, 1f);
        }

        // Sprite art natively faces LEFT (spriteFacesLeft: true): moving left
        // shows the sprite un-mirrored, moving right mirrors it.
        [TestCase(-1, false, true, false)]
        [TestCase(-1, true, true, false)]
        [TestCase(1, false, true, true)]
        [TestCase(1, true, true, true)]
        [TestCase(0, true, true, true)]
        [TestCase(0, false, true, false)]
        // Right-facing art (spriteFacesLeft: false) mirrors the other way.
        [TestCase(-1, false, false, true)]
        [TestCase(1, true, false, false)]
        [TestCase(0, true, false, true)]
        public void ComputeFlipX_MirrorsAgainstNativeFacing_KeepsFacingOnVertical(
            int directionX, bool currentFlipX, bool spriteFacesLeft, bool expected)
        {
            bool value = PlayerMotionAnimator.ComputeFlipX(directionX, currentFlipX, spriteFacesLeft);

            Assert.AreEqual(expected, value);
        }
    }
}
