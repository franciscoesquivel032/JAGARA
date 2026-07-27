using UnityEngine;

namespace Jagara.Runtime.Gameplay
{
    /// <summary>
    /// Pure math for the player's procedural motion (idle bob, move hop,
    /// squash-stretch, facing), decoupled from MonoBehaviour so it can run in
    /// Edit Mode tests. See PlayerVisualAnimator for the runtime wrapper.
    /// </summary>
    public static class PlayerMotionAnimator
    {
        // Breathing squash: y oscillates in [1 - amount, 1] (never stretches past
        // the sprite rect) while x widens inversely to preserve apparent volume.
        public static Vector2 ComputeIdleBreathScale(float time, float amount, float speed)
        {
            float squash = amount * 0.5f * (1f - Mathf.Sin(time * speed));
            return new Vector2(1f + squash, 1f - squash);
        }

        // Vertical offset that keeps the sprite's bottom edge fixed while its
        // y scale changes (center pivot): negative when squashed.
        public static float ComputeGroundedOffset(float scaleY, float spriteHeight)
        {
            return (scaleY - 1f) * 0.5f * spriteHeight;
        }

        public static float ComputeHopHeight(float progress, float hopHeight)
        {
            float p = Mathf.Clamp01(progress);
            return 4f * hopHeight * p * (1f - p);
        }

        public static Vector2 ComputeHopScale(float progress, float squashAmount)
        {
            float p = Mathf.Clamp01(progress);
            float stretch = squashAmount * Mathf.Sin(p * Mathf.PI);
            return new Vector2(1f - stretch, 1f + stretch);
        }

        public static bool ComputeFlipX(int directionX, bool currentFlipX, bool spriteFacesLeft)
        {
            if (directionX == 0)
            {
                return currentFlipX;
            }

            bool movingLeft = directionX < 0;
            return spriteFacesLeft ? !movingLeft : movingLeft;
        }
    }
}
