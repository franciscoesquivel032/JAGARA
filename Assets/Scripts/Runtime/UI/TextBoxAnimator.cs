using UnityEngine;

namespace Jagara.Runtime.UI
{
    /// <summary>
    /// The text box's animation math, kept pure and static so it can be tested in
    /// Edit Mode - the same split PulseAnimator and PlayerMotionAnimator use. See
    /// TextBoxController for the runtime wrapper that drives it from Update().
    /// <para>
    /// Slot indexing convention shared with TextBoxController: slot 0 is the
    /// outgoing line - the one that has just been pushed off the top of the box
    /// and is sliding out under the mask - and slots 1..n hold the visible window,
    /// oldest first. So the newest line always sits at slot <c>filledCount</c>.
    /// </para>
    /// </summary>
    public static class TextBoxAnimator
    {
        /// <summary>
        /// Height the box should settle at for <paramref name="lineCount"/> lines.
        /// With no lines this collapses to just the padding rather than zero, so a
        /// box that is about to reappear grows out of a sliver instead of popping
        /// open at full size.
        /// </summary>
        public static float ComputeTargetHeight(int lineCount, int maxLines, float lineHeight, float verticalPadding)
        {
            int shown = Mathf.Clamp(lineCount, 0, Mathf.Max(0, maxLines));
            return shown * lineHeight + verticalPadding;
        }

        /// <summary>
        /// How far the line container has travelled up, in pixels, at
        /// <paramref name="progress"/> through the slide. Runs from 0 (the moment
        /// the new line arrives, content still sitting where the eye last saw it)
        /// to a full <paramref name="lineHeight"/> at rest.
        /// </summary>
        public static float ComputeSlideOffset(float progress, float lineHeight) =>
            Ease(Mathf.Clamp01(progress)) * lineHeight;

        /// <summary>
        /// Target opacity for a slot. <paramref name="alphaByAge"/> is indexed by
        /// age, newest first (e.g. {1, 0.6, 0.35}); ages past the end of the array
        /// reuse its last entry, so shortening the array dims rather than breaks.
        /// Empty slots and the outgoing slot 0 are fully transparent.
        /// </summary>
        public static float ComputeLineAlpha(int slotIndex, int filledCount, float[] alphaByAge)
        {
            if (slotIndex < 1 || slotIndex > filledCount)
            {
                return 0f;
            }

            if (alphaByAge == null || alphaByAge.Length == 0)
            {
                return 1f;
            }

            int age = filledCount - slotIndex;
            return alphaByAge[Mathf.Min(age, alphaByAge.Length - 1)];
        }

        /// <summary>
        /// Framerate-independent exponential approach towards
        /// <paramref name="target"/>. Unlike a raw <c>Lerp(a, b, speed * dt)</c>
        /// this converges at the same rate whatever the frame time, which matters
        /// because the box animates during turn resolution where frame times spike.
        /// </summary>
        public static float Approach(float current, float target, float speed, float deltaTime)
        {
            if (speed <= 0f || deltaTime <= 0f)
            {
                return current;
            }

            return Mathf.Lerp(current, target, 1f - Mathf.Exp(-speed * deltaTime));
        }

        /// <summary>Smoothstep: eases in and out, so the slide has no hard start or stop.</summary>
        public static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}
