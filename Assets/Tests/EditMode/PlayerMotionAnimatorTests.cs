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

        [TestCase(0f)]
        [TestCase(1f)]
        public void ComputeAttackLungeOffset_IsZero_AtStartAndEnd(float progress)
        {
            Vector2 offset = PlayerMotionAnimator.ComputeAttackLungeOffset(progress, Vector2.right, lungeDistance: 0.35f);

            Assert.AreEqual(0f, offset.x, 1e-5f);
            Assert.AreEqual(0f, offset.y, 1e-5f);
        }

        [Test]
        public void ComputeAttackLungeOffset_PeaksAtMidpoint_AlongDirection()
        {
            const float lungeDistance = 0.35f;

            Vector2 apex = PlayerMotionAnimator.ComputeAttackLungeOffset(0.5f, Vector2.right, lungeDistance);
            Vector2 rising = PlayerMotionAnimator.ComputeAttackLungeOffset(0.25f, Vector2.right, lungeDistance);
            Vector2 falling = PlayerMotionAnimator.ComputeAttackLungeOffset(0.75f, Vector2.right, lungeDistance);

            Assert.AreEqual(lungeDistance, apex.x, 1e-5f);
            Assert.AreEqual(0f, apex.y, 1e-5f);
            Assert.Greater(apex.x, rising.x);
            Assert.Greater(apex.x, falling.x);
        }

        [Test]
        public void ComputeAttackLungeOffset_ScalesAlongGivenDirection()
        {
            Vector2 right = PlayerMotionAnimator.ComputeAttackLungeOffset(0.5f, Vector2.right, 0.35f);
            Vector2 up = PlayerMotionAnimator.ComputeAttackLungeOffset(0.5f, Vector2.up, 0.35f);
            Vector2 left = PlayerMotionAnimator.ComputeAttackLungeOffset(0.5f, Vector2.left, 0.35f);

            Assert.AreEqual(0.35f, right.x, 1e-5f);
            Assert.AreEqual(0f, right.y, 1e-5f);
            Assert.AreEqual(0.35f, up.y, 1e-5f);
            Assert.AreEqual(0f, up.x, 1e-5f);
            Assert.AreEqual(-0.35f, left.x, 1e-5f);
            Assert.AreEqual(0f, left.y, 1e-5f);
        }

        [Test]
        public void ComputeHitShakeOffset_IsZero_AtStartAndEnd()
        {
            float start = PlayerMotionAnimator.ComputeHitShakeOffset(0f, magnitude: 0.08f);
            float end = PlayerMotionAnimator.ComputeHitShakeOffset(1f, magnitude: 0.08f);

            Assert.AreEqual(0f, start, 1e-5f);
            Assert.AreEqual(0f, end, 1e-5f);
        }

        [Test]
        public void ComputeHitShakeOffset_IsDeterministic_ForSameInputs()
        {
            float a = PlayerMotionAnimator.ComputeHitShakeOffset(0.4f, 0.08f);
            float b = PlayerMotionAnimator.ComputeHitShakeOffset(0.4f, 0.08f);

            Assert.AreEqual(a, b);
        }

        [Test]
        public void ComputeHitShakeOffset_AmplitudeDecaysAsProgressAdvances()
        {
            // Both samples land on a quarter-cycle peak (sin = 1 exactly, given
            // the fixed 3-oscillation curve), so only the (1 - progress) decay
            // factor differs between them - the later sample must be smaller.
            const float magnitude = 0.08f;
            float early = Mathf.Abs(PlayerMotionAnimator.ComputeHitShakeOffset(1f / 12f, magnitude));
            float late = Mathf.Abs(PlayerMotionAnimator.ComputeHitShakeOffset(1f / 12f + 2f / 3f, magnitude));

            Assert.Greater(early, late);
        }

        [Test]
        public void ComputeHitShakeOffset_OscillatesThreeTimesAcrossFullDuration()
        {
            // With ShakeCycles baked at 3, sin(6*pi*p) crosses zero at p = k/6 for
            // integer k, including p = 0.5 (k=3) - a curve with fewer oscillations
            // (e.g. 1 cycle) would still be solidly positive at p = 0.5, so this
            // pins the oscillation count without hardcoding the private constant.
            float atHalf = PlayerMotionAnimator.ComputeHitShakeOffset(0.5f, magnitude: 0.08f);

            Assert.AreEqual(0f, atHalf, 1e-4f);
        }

        [TestCase(0f, 1f)]
        [TestCase(1f, 0f)]
        [TestCase(0.5f, 0.5f)]
        public void ComputeHitFlashIntensity_FadesLinearlyFromOneToZero(float progress, float expected)
        {
            float intensity = PlayerMotionAnimator.ComputeHitFlashIntensity(progress);

            Assert.AreEqual(expected, intensity, 1e-5f);
        }

        [TestCase(-0.5f, 1f)]
        [TestCase(1.5f, 0f)]
        public void ComputeHitFlashIntensity_ClampsOutOfRangeProgress(float progress, float expected)
        {
            float intensity = PlayerMotionAnimator.ComputeHitFlashIntensity(progress);

            Assert.AreEqual(expected, intensity, 1e-5f);
        }
    }
}
