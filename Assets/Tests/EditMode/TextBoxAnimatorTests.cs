using NUnit.Framework;
using Jagara.Runtime.UI;

namespace Jagara.Tests.EditMode
{
    /// <summary>
    /// The text box's animation math is pure and static precisely so its edge
    /// cases - a window that is not full, a slot array shorter than the alpha
    /// ramp, a frame time spike mid-slide - can be pinned down here instead of
    /// being eyeballed in Play Mode.
    /// </summary>
    public class TextBoxAnimatorTests
    {
        private const float LineHeight = 30f;
        private const float Padding = 24f;

        private static readonly float[] Alphas = { 1f, 0.6f, 0.35f };

        [Test]
        public void ComputeTargetHeight_WithNoLines_CollapsesToPaddingOnly()
        {
            Assert.AreEqual(Padding, TextBoxAnimator.ComputeTargetHeight(0, 3, LineHeight, Padding));
        }

        [Test]
        public void ComputeTargetHeight_GrowsOneLineAtATime()
        {
            Assert.AreEqual(Padding + LineHeight, TextBoxAnimator.ComputeTargetHeight(1, 3, LineHeight, Padding));
            Assert.AreEqual(Padding + 2f * LineHeight, TextBoxAnimator.ComputeTargetHeight(2, 3, LineHeight, Padding));
        }

        [Test]
        public void ComputeTargetHeight_StopsGrowingAtTheVisibleLineCount()
        {
            float full = TextBoxAnimator.ComputeTargetHeight(3, 3, LineHeight, Padding);

            // The window is trimmed elsewhere, but the box must not stretch even if
            // it is ever asked to show more lines than it has slots for.
            Assert.AreEqual(full, TextBoxAnimator.ComputeTargetHeight(7, 3, LineHeight, Padding));
        }

        [Test]
        public void ComputeSlideOffset_RunsFromZeroToAFullLine()
        {
            Assert.AreEqual(0f, TextBoxAnimator.ComputeSlideOffset(0f, LineHeight));
            Assert.AreEqual(LineHeight, TextBoxAnimator.ComputeSlideOffset(1f, LineHeight));
        }

        [Test]
        public void ComputeSlideOffset_ClampsBeyondTheEndsOfTheSlide()
        {
            Assert.AreEqual(0f, TextBoxAnimator.ComputeSlideOffset(-0.5f, LineHeight));
            Assert.AreEqual(LineHeight, TextBoxAnimator.ComputeSlideOffset(3f, LineHeight));
        }

        [Test]
        public void ComputeSlideOffset_MovesInOneDirectionOnly()
        {
            float previous = -1f;
            for (int step = 0; step <= 10; step++)
            {
                float offset = TextBoxAnimator.ComputeSlideOffset(step / 10f, LineHeight);
                Assert.Greater(offset, previous, "the stack must never slide back down mid-animation");
                previous = offset;
            }
        }

        [Test]
        public void ComputeLineAlpha_OutgoingSlotIsTransparent()
        {
            // Slot 0 holds the line being pushed off the top; it is only ever on its
            // way out, so its target is always fully faded.
            Assert.AreEqual(0f, TextBoxAnimator.ComputeLineAlpha(0, 3, Alphas));
        }

        [Test]
        public void ComputeLineAlpha_EmptySlotsAreTransparent()
        {
            Assert.AreEqual(0f, TextBoxAnimator.ComputeLineAlpha(2, 1, Alphas),
                "with a single message only slot 1 is in use");
        }

        [Test]
        public void ComputeLineAlpha_NewestLineIsFullyBright_AndOlderOnesDim()
        {
            Assert.AreEqual(1f, TextBoxAnimator.ComputeLineAlpha(3, 3, Alphas), "newest");
            Assert.AreEqual(0.6f, TextBoxAnimator.ComputeLineAlpha(2, 3, Alphas), "one turn old");
            Assert.AreEqual(0.35f, TextBoxAnimator.ComputeLineAlpha(1, 3, Alphas), "oldest");
        }

        [Test]
        public void ComputeLineAlpha_NewestIsBrightEvenBeforeTheWindowFills()
        {
            Assert.AreEqual(1f, TextBoxAnimator.ComputeLineAlpha(1, 1, Alphas));
            Assert.AreEqual(1f, TextBoxAnimator.ComputeLineAlpha(2, 2, Alphas));
            Assert.AreEqual(0.6f, TextBoxAnimator.ComputeLineAlpha(1, 2, Alphas));
        }

        [Test]
        public void ComputeLineAlpha_AgesPastTheRampReuseItsLastEntry()
        {
            float[] shortRamp = { 1f, 0.5f };

            Assert.AreEqual(0.5f, TextBoxAnimator.ComputeLineAlpha(1, 4, shortRamp),
                "a ramp shorter than the window dims the extra lines rather than breaking");
        }

        [Test]
        public void ComputeLineAlpha_WithNoRamp_LeavesEveryLineFullyBright()
        {
            Assert.AreEqual(1f, TextBoxAnimator.ComputeLineAlpha(1, 3, null));
            Assert.AreEqual(1f, TextBoxAnimator.ComputeLineAlpha(1, 3, new float[0]));
        }

        [Test]
        public void Approach_MovesTowardsTheTargetWithoutOvershooting()
        {
            float value = TextBoxAnimator.Approach(0f, 100f, 18f, 0.016f);

            Assert.Greater(value, 0f);
            Assert.Less(value, 100f);
        }

        [Test]
        public void Approach_WithNoTimeElapsed_DoesNotMove()
        {
            Assert.AreEqual(42f, TextBoxAnimator.Approach(42f, 100f, 18f, 0f));
        }

        [Test]
        public void Approach_ConvergesOnTheTarget()
        {
            float value = 0f;
            for (int frame = 0; frame < 120; frame++)
            {
                value = TextBoxAnimator.Approach(value, 100f, 18f, 0.016f);
            }

            Assert.AreEqual(100f, value, 0.01f);
        }

        [Test]
        public void Approach_IsFramerateIndependent()
        {
            float oneBigStep = TextBoxAnimator.Approach(0f, 100f, 18f, 0.1f);

            float twoSmallSteps = TextBoxAnimator.Approach(0f, 100f, 18f, 0.05f);
            twoSmallSteps = TextBoxAnimator.Approach(twoSmallSteps, 100f, 18f, 0.05f);

            Assert.AreEqual(oneBigStep, twoSmallSteps, 0.001f,
                "the box must settle at the same rate whatever the frame time");
        }
    }
}
