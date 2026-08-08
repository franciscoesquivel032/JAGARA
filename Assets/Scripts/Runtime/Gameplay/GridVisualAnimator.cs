using System;
using System.Collections;
using UnityEngine;

namespace Jagara.Runtime.Gameplay
{
    /// <summary>
    /// Drives an entity's procedural animation on the Visual child: idle
    /// breathing bob while stationary, a hop with squash-stretch during the
    /// grid tween, an attack lunge-and-return, a hit-reaction flash+shake
    /// overlay, and a death blink-then-vanish sequence - plus flipX facing.
    /// Only touches the child's localPosition/localScale, a shared
    /// MaterialPropertyBlock (for the flash), and the SpriteRenderer's
    /// enabled state (for the death blink), so it never fights GridMover,
    /// which tweens the root. Math lives in PlayerMotionAnimator (Edit Mode
    /// testable).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class GridVisualAnimator : MonoBehaviour
    {
        private static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");
        private static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");

        [SerializeField] private GridMover mover;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private float idleBreathAmount = 0.03125f;
        [SerializeField] private float idleBreathSpeed = 2.5f;
        [SerializeField] private float hopHeight = 0.125f;
        [SerializeField] private float squashAmount = 0.06f;
        [SerializeField] private bool spriteFacesLeft = true;

        [Header("Attack lunge")]
        [SerializeField] private float attackLungeDistance = 0.35f;
        [SerializeField] private float attackDuration = 0.24f;

        [Header("Hit reaction")]
        [SerializeField] private float hitShakeMagnitude = 0.08f;
        [SerializeField] private float hitDuration = 0.33f;
        [SerializeField] private Color hitFlashColor = Color.white;
        [SerializeField] private ParticleSystem hitParticles;

        [Header("Death")]
        [SerializeField] private int deathBlinkCount = 5;
        [SerializeField] private float deathBlinkInterval = 0.06f;

        private Vector3 baseLocalPosition;
        private float spriteHeight;
        private MaterialPropertyBlock propertyBlock;

        // Attack lunge state - a third, mutually exclusive pose alongside
        // idle/move-hop (see LateUpdate).
        private bool isAttacking;
        private float attackProgress;
        private Vector2Int attackDirection;
        private Action attackCompleteCallback;
        private Coroutine attackCoroutine;

        // Hit-reaction state - an overlay on top of whichever pose is
        // active, not a pose of its own.
        private bool isHit;
        private float hitProgress;
        private Coroutine hitCoroutine;

        // Death state - once true, LateUpdate stops driving pose/flash
        // entirely (see below); the corpse just blinks until destroyed.
        private bool isDying;
        private Action deathCompleteCallback;
        private Coroutine deathCoroutine;

        private void Awake()
        {
            if (mover == null)
            {
                mover = GetComponentInParent<GridMover>();
            }

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            if (mover == null || spriteRenderer == null)
            {
                Debug.LogError("GridVisualAnimator: missing GridMover in parents or SpriteRenderer on this GameObject.");
                enabled = false;
                return;
            }

            baseLocalPosition = transform.localPosition;
            propertyBlock = new MaterialPropertyBlock();
            RefreshSpriteMetrics();
        }

        /// <summary>
        /// Recomputes cached sprite-derived metrics (currently spriteHeight)
        /// from the SpriteRenderer's current sprite. Awake() caches these
        /// once, but callers that reassign spriteRenderer.sprite after Awake
        /// (e.g. EnemyController.Initialize applying a per-config sprite)
        /// must call this afterward or the cached metric goes stale.
        /// </summary>
        public void RefreshSpriteMetrics()
        {
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

            // Coroutines are already stopped implicitly on disable, but their
            // state flags and pending callback are not cleared automatically -
            // do that here so a later re-enable doesn't resume a stale pose,
            // and so a lunge disabled mid-flight never fires a stale callback.
            if (attackCoroutine != null)
            {
                StopCoroutine(attackCoroutine);
                attackCoroutine = null;
                isAttacking = false;

                Action callback = attackCompleteCallback;
                attackCompleteCallback = null;
                callback?.Invoke();
            }

            if (hitCoroutine != null)
            {
                StopCoroutine(hitCoroutine);
                hitCoroutine = null;
                isHit = false;
                ResetFlash();
            }

            if (deathCoroutine != null)
            {
                StopCoroutine(deathCoroutine);
                deathCoroutine = null;
                isDying = false;
                spriteRenderer.enabled = false;

                Action callback = deathCompleteCallback;
                deathCompleteCallback = null;
                callback?.Invoke();
            }
        }

        private void HandleMoveStarted(Vector2Int direction)
        {
            spriteRenderer.flipX = PlayerMotionAnimator.ComputeFlipX(direction.x, spriteRenderer.flipX, spriteFacesLeft);
        }

        /// <summary>
        /// Plays a bump-and-return lunge toward <paramref name="direction"/> (a unit
        /// cardinal vector), setting facing immediately. Calls <paramref name="onComplete"/>
        /// once the lunge has returned to rest - callers (PlayerController,
        /// EnemyController) use this to know when it's safe to end the turn.
        /// Restarting mid-lunge stops the previous coroutine first.
        /// </summary>
        public void PlayAttack(Vector2Int direction, Action onComplete = null)
        {
            if (attackCoroutine != null)
            {
                StopCoroutine(attackCoroutine);
            }

            attackDirection = direction;
            spriteRenderer.flipX = PlayerMotionAnimator.ComputeFlipX(direction.x, spriteRenderer.flipX, spriteFacesLeft);
            attackCompleteCallback = onComplete;
            attackCoroutine = StartCoroutine(AttackRoutine());
        }

        /// <summary>
        /// Plays a flash+shake hit reaction. Purely cosmetic - never reports
        /// completion and never blocks anything. Restarting mid-reaction stops
        /// the previous coroutine first.
        /// </summary>
        public void PlayHitReaction()
        {
            if (hitCoroutine != null)
            {
                StopCoroutine(hitCoroutine);
            }

            hitParticles?.Play();
            hitCoroutine = StartCoroutine(HitReactionRoutine());
        }

        /// <summary>
        /// Plays a blink-then-vanish death animation: toggles the sprite's
        /// visibility on/off for deathBlinkCount cycles, then calls
        /// onComplete - callers (EnemyController.Die) use this to know when
        /// it's safe to actually destroy the GameObject. Cancels any
        /// in-flight hit-reaction first, since a killing blow already fires
        /// HealthState.OnHPChanged (and therefore PlayHitReaction) before
        /// OnDeath calls this. Purely cosmetic beyond that callback - does
        /// not touch TurnResolver or gate the turn in any way.
        /// </summary>
        public void PlayDeath(Action onComplete = null)
        {
            if (isDying)
            {
                return;
            }

            if (hitCoroutine != null)
            {
                StopCoroutine(hitCoroutine);
                hitCoroutine = null;
                isHit = false;
                ResetFlash();
            }

            isDying = true;
            transform.localPosition = baseLocalPosition;
            transform.localScale = Vector3.one;
            deathCompleteCallback = onComplete;
            deathCoroutine = StartCoroutine(DeathRoutine());
        }

        private IEnumerator AttackRoutine()
        {
            isAttacking = true;
            float elapsed = 0f;

            while (elapsed < attackDuration)
            {
                elapsed += Time.deltaTime;
                attackProgress = Mathf.Clamp01(elapsed / attackDuration);
                yield return null;
            }

            attackProgress = 1f;
            isAttacking = false;
            attackCoroutine = null;

            Action callback = attackCompleteCallback;
            attackCompleteCallback = null;
            callback?.Invoke();
        }

        private IEnumerator HitReactionRoutine()
        {
            isHit = true;
            float elapsed = 0f;

            while (elapsed < hitDuration)
            {
                elapsed += Time.deltaTime;
                hitProgress = Mathf.Clamp01(elapsed / hitDuration);
                yield return null;
            }

            isHit = false;
            hitCoroutine = null;
            ResetFlash();
        }

        private IEnumerator DeathRoutine()
        {
            int totalToggles = deathBlinkCount * 2;
            var blinkWait = new WaitForSeconds(deathBlinkInterval);

            for (int i = 0; i < totalToggles; i++)
            {
                spriteRenderer.enabled = !spriteRenderer.enabled;
                yield return blinkWait;
            }

            spriteRenderer.enabled = false;
            deathCoroutine = null;

            Action callback = deathCompleteCallback;
            deathCompleteCallback = null;
            callback?.Invoke();
        }

        /// <summary>
        /// Zeroes the shared MaterialPropertyBlock's flash amount without
        /// touching any other property another component (e.g. EntityOutline's
        /// _OutlineColor) has already written into the same block - always
        /// read-modify-write via GetPropertyBlock, never construct a fresh
        /// block and set it unconditionally.
        /// </summary>
        private void ResetFlash()
        {
            spriteRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(FlashAmountId, 0f);
            spriteRenderer.SetPropertyBlock(propertyBlock);
        }

        // LateUpdate so the mover's coroutine (which runs after Update) has
        // already written this frame's MoveProgress before we sample it.
        private void LateUpdate()
        {
            if (isDying)
            {
                return;
            }

            Vector3 baseOffset;
            Vector2 scale;

            if (mover.IsMoving)
            {
                baseOffset = new Vector3(0f, PlayerMotionAnimator.ComputeHopHeight(mover.MoveProgress, hopHeight), 0f);
                scale = PlayerMotionAnimator.ComputeHopScale(mover.MoveProgress, squashAmount);
            }
            else if (isAttacking)
            {
                Vector2 lunge = PlayerMotionAnimator.ComputeAttackLungeOffset(attackProgress, attackDirection, attackLungeDistance);
                baseOffset = new Vector3(lunge.x, lunge.y, 0f);
                scale = PlayerMotionAnimator.ComputeHopScale(attackProgress, squashAmount);
            }
            else
            {
                scale = PlayerMotionAnimator.ComputeIdleBreathScale(Time.time, idleBreathAmount, idleBreathSpeed);
                baseOffset = new Vector3(0f, PlayerMotionAnimator.ComputeGroundedOffset(scale.y, spriteHeight), 0f);
            }

            Vector3 hitOffset = Vector3.zero;
            if (isHit)
            {
                hitOffset = new Vector3(PlayerMotionAnimator.ComputeHitShakeOffset(hitProgress, hitShakeMagnitude), 0f, 0f);

                spriteRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(FlashColorId, hitFlashColor);
                propertyBlock.SetFloat(FlashAmountId, PlayerMotionAnimator.ComputeHitFlashIntensity(hitProgress));
                spriteRenderer.SetPropertyBlock(propertyBlock);
            }

            transform.localPosition = baseLocalPosition + baseOffset + hitOffset;
            transform.localScale = new Vector3(scale.x, scale.y, 1f);
        }
    }
}
