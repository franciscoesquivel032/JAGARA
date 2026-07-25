using UnityEngine;

namespace Jagara.Runtime.Gameplay
{
    /// <summary>
    /// Drives the player's procedural animation on the Visual child: idle
    /// breathing bob while stationary, a hop with squash-stretch during the
    /// grid tween, and flipX facing. Only touches the child's localPosition
    /// and localScale, so it never fights PlayerGridMover, which tweens the
    /// root. Math lives in PlayerMotionAnimator (Edit Mode testable).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class PlayerVisualAnimator : MonoBehaviour
    {
        [SerializeField] private PlayerGridMover mover;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private float idleBreathAmount = 0.03125f;
        [SerializeField] private float idleBreathSpeed = 2.5f;
        [SerializeField] private float hopHeight = 0.125f;
        [SerializeField] private float squashAmount = 0.06f;
        [SerializeField] private bool spriteFacesLeft = true;

        private Vector3 baseLocalPosition;
        private float spriteHeight;

        private void Awake()
        {
            if (mover == null)
            {
                mover = GetComponentInParent<PlayerGridMover>();
            }

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            if (mover == null || spriteRenderer == null)
            {
                Debug.LogError("PlayerVisualAnimator: missing PlayerGridMover in parents or SpriteRenderer on this GameObject.");
                enabled = false;
                return;
            }

            baseLocalPosition = transform.localPosition;
            spriteHeight = spriteRenderer.sprite != null ? spriteRenderer.sprite.bounds.size.y : 1f;
        }

        private void OnEnable()
        {
            if (mover != null)
            {
                mover.OnMoveStarted += HandleMoveStarted;
            }
        }

        private void OnDisable()
        {
            if (mover != null)
            {
                mover.OnMoveStarted -= HandleMoveStarted;
            }
        }

        private void HandleMoveStarted(Vector2Int direction)
        {
            spriteRenderer.flipX = PlayerMotionAnimator.ComputeFlipX(direction.x, spriteRenderer.flipX, spriteFacesLeft);
        }

        // LateUpdate so the mover's coroutine (which runs after Update) has
        // already written this frame's MoveProgress before we sample it.
        private void LateUpdate()
        {
            float yOffset;
            Vector2 scale;

            if (mover.IsMoving)
            {
                yOffset = PlayerMotionAnimator.ComputeHopHeight(mover.MoveProgress, hopHeight);
                scale = PlayerMotionAnimator.ComputeHopScale(mover.MoveProgress, squashAmount);
            }
            else
            {
                scale = PlayerMotionAnimator.ComputeIdleBreathScale(Time.time, idleBreathAmount, idleBreathSpeed);
                yOffset = PlayerMotionAnimator.ComputeGroundedOffset(scale.y, spriteHeight);
            }

            transform.localPosition = baseLocalPosition + new Vector3(0f, yOffset, 0f);
            transform.localScale = new Vector3(scale.x, scale.y, 1f);
        }
    }
}
