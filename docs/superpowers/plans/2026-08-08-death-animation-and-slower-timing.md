# Death Animation, True White Hit-Flash, and Slower Timing Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the hit-reaction's color-tint flash (which can never reach true white under multiply blending) with a genuine shader-driven white flash, add a blink-then-vanish death animation for enemies, and slow down the existing attack-lunge/hit-reaction timings by ~1.5x.

**Architecture:** Extends `EntityFactionOutline.shader` with `_FlashColor`/`_FlashAmount` uniforms that lerp (not multiply) the sampled sprite color, driven per-instance through the same `MaterialPropertyBlock` mechanism `EntityOutline.cs` already uses on the same GameObject (read-modify-write via `GetPropertyBlock`, never a fresh block, so the two components' properties never clobber each other). `GridVisualAnimator` drives this instead of `SpriteRenderer.color`, and gains a `PlayDeath` coroutine that toggles `SpriteRenderer.enabled` on/off before reporting completion. `EnemyController.Die()` defers `Destroy(gameObject)` to that completion callback while still freeing the turn/grid state immediately.

**Tech Stack:** Unity 6000.5.3f1, URP 2D Renderer, HLSL (Core.hlsl / ShaderLab), C#, MCP for Unity (Play Mode verification — no Edit Mode test infra exists for shaders in this project).

## Global Constraints

- No changes to `CombatResolver`, `HealthState`, or damage math.
- No player death animation — the player's death stays a narrative beat (Voice dialogue), unaffected by this change.
- No changes to the outline pass, blending mode, or any other existing behavior of `EntityFactionOutline.shader` beyond the two new flash properties.
- `EnemyControllerTests` must continue to pass unmodified — no test in that file calls `Die()` today (confirmed via grep), so this plan is not expected to require any change to that test file.
- Always read-modify-write the shared `MaterialPropertyBlock` (`GetPropertyBlock` before `SetPropertyBlock`) — never construct a fresh block and set it unconditionally, or `EntityOutline`'s `_OutlineColor` would be silently wiped.
- Full spec: `docs/superpowers/specs/2026-08-08-death-animation-and-slower-timing-design.md`.

---

### Task 1: True white hit-flash (shader + `GridVisualAnimator`) and slower timing

**Files:**
- Modify: `Assets/Art/Shaders/EntityFactionOutline.shader`
- Modify: `Assets/Scripts/Runtime/Gameplay/GridVisualAnimator.cs`

**Interfaces:**
- Consumes: `Shader.PropertyToID`, `SpriteRenderer.GetPropertyBlock`/`SetPropertyBlock`/`MaterialPropertyBlock` (standard Unity API); `PlayerMotionAnimator.ComputeHitFlashIntensity(float) -> float` (already exists, unchanged).
- Produces (unchanged public signatures, used by Task 2 and by existing callers `PlayerController`/`EnemyController`): `GridVisualAnimator.PlayAttack(Vector2Int, Action)`, `GridVisualAnimator.PlayHitReaction()`. A new private helper `ResetFlash()` is introduced for Task 2 to reuse.

- [ ] **Step 1: Edit the shader**

Open `Assets/Art/Shaders/EntityFactionOutline.shader`.

Find the `Properties` block:

```hlsl
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _OutlineColor ("Outline Color", Color) = (1, 1, 1, 1)
        _OutlineWidth ("Outline Width (texels)", Range(0, 2)) = 0.5
        _OutlineIntensity ("Outline Intensity", Range(0, 1)) = 0.55
    }
```

Replace with:

```hlsl
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _OutlineColor ("Outline Color", Color) = (1, 1, 1, 1)
        _OutlineWidth ("Outline Width (texels)", Range(0, 2)) = 0.5
        _OutlineIntensity ("Outline Intensity", Range(0, 1)) = 0.55
        _FlashColor ("Flash Color", Color) = (1, 1, 1, 1)
        _FlashAmount ("Flash Amount", Range(0, 1)) = 0
    }
```

Find the `CBUFFER`:

```hlsl
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _MainTex_TexelSize;
                half4 _Color;
                half4 _OutlineColor;
                float _OutlineWidth;
                float _OutlineIntensity;
            CBUFFER_END
```

Replace with:

```hlsl
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _MainTex_TexelSize;
                half4 _Color;
                half4 _OutlineColor;
                float _OutlineWidth;
                float _OutlineIntensity;
                half4 _FlashColor;
                float _FlashAmount;
            CBUFFER_END
```

Find the opaque-path branch in `frag()`:

```hlsl
            half4 frag(Varyings IN) : SV_Target
            {
                half4 baseSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * IN.color;

                if (baseSample.a > 0.001)
                {
                    return baseSample;
                }
```

Replace with:

```hlsl
            half4 frag(Varyings IN) : SV_Target
            {
                half4 baseSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * IN.color;

                if (baseSample.a > 0.001)
                {
                    baseSample.rgb = lerp(baseSample.rgb, _FlashColor.rgb, _FlashAmount);
                    return baseSample;
                }
```

Do not touch the outline branch (the code after this `if` block) — only the sprite body flashes, not the outline silhouette.

- [ ] **Step 2: Replace `GridVisualAnimator.cs`**

Overwrite `Assets/Scripts/Runtime/Gameplay/GridVisualAnimator.cs` with:

```csharp
using System;
using System.Collections;
using UnityEngine;

namespace Jagara.Runtime.Gameplay
{
    /// <summary>
    /// Drives an entity's procedural animation on the Visual child: idle
    /// breathing bob while stationary, a hop with squash-stretch during the
    /// grid tween, an attack lunge-and-return, and a hit-reaction
    /// flash+shake overlay - plus flipX facing. Only touches the child's
    /// localPosition/localScale and a shared MaterialPropertyBlock (for the
    /// flash), so it never fights GridMover, which tweens the root. Math
    /// lives in PlayerMotionAnimator (Edit Mode testable).
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

            hitCoroutine = StartCoroutine(HitReactionRoutine());
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
```

Note what changed from the previous version: `originalColor` and every direct read/write of `spriteRenderer.color` for the flash are gone, replaced by the `propertyBlock`/`ResetFlash()` mechanism; `hitFlashColor` defaults to `Color.white`; `attackDuration` is `0.24f` (was `0.16f`); `hitDuration` is `0.33f` (was `0.22f`).

- [ ] **Step 3: Verify it compiles with no console errors**

Use `mcp__UnityMCP__refresh_unity` to trigger a recompile (this also recompiles the shader), then `mcp__UnityMCP__read_console` (errors only) to confirm there are no shader compile errors or C# compile errors from this change. Quote the actual output in your report.

- [ ] **Step 4: Commit**

```bash
git add "Assets/Art/Shaders/EntityFactionOutline.shader" "Assets/Scripts/Runtime/Gameplay/GridVisualAnimator.cs"
git commit -m "Replace hit-flash color tint with a true shader-driven white flash

- EntityFactionOutline.shader gains _FlashColor/_FlashAmount, lerping
  (not multiplying) the sampled sprite color so it can actually reach
  white, unlike the previous SpriteRenderer.color tint
- GridVisualAnimator drives the new properties through the same
  MaterialPropertyBlock EntityOutline already uses on the same
  GameObject, read-modify-write so neither clobbers the other
- hitFlashColor reverts to white now that it actually works
- attackDuration 0.16s->0.24s, hitDuration 0.22s->0.33s (~1.5x slower)"
```

---

### Task 2: Enemy death animation

**Files:**
- Modify: `Assets/Scripts/Runtime/Gameplay/GridVisualAnimator.cs` (builds on Task 1's version)
- Modify: `Assets/Scripts/Runtime/Enemies/EnemyController.cs`

**Interfaces:**
- Consumes: `GridVisualAnimator`'s Task-1 API (`PlayHitReaction`, the private `ResetFlash` helper is reused internally, not exposed).
- Produces: `public void GridVisualAnimator.PlayDeath(Action onComplete = null)` — used by `EnemyController.Die()`.

- [ ] **Step 1: Add death state fields to `GridVisualAnimator`**

In `Assets/Scripts/Runtime/Gameplay/GridVisualAnimator.cs`, find the `[Header("Hit reaction")]` field block:

```csharp
        [Header("Hit reaction")]
        [SerializeField] private float hitShakeMagnitude = 0.08f;
        [SerializeField] private float hitDuration = 0.33f;
        [SerializeField] private Color hitFlashColor = Color.white;
```

Replace with:

```csharp
        [Header("Hit reaction")]
        [SerializeField] private float hitShakeMagnitude = 0.08f;
        [SerializeField] private float hitDuration = 0.33f;
        [SerializeField] private Color hitFlashColor = Color.white;

        [Header("Death")]
        [SerializeField] private int deathBlinkCount = 5;
        [SerializeField] private float deathBlinkInterval = 0.06f;
```

Find the hit-reaction state field block:

```csharp
        // Hit-reaction state - an overlay on top of whichever pose is
        // active, not a pose of its own.
        private bool isHit;
        private float hitProgress;
        private Coroutine hitCoroutine;
```

Replace with:

```csharp
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
```

- [ ] **Step 2: Add `PlayDeath` and `DeathRoutine`**

Find `PlayHitReaction`:

```csharp
        public void PlayHitReaction()
        {
            if (hitCoroutine != null)
            {
                StopCoroutine(hitCoroutine);
            }

            hitCoroutine = StartCoroutine(HitReactionRoutine());
        }
```

Replace with (adds `PlayDeath` immediately after):

```csharp
        public void PlayHitReaction()
        {
            if (hitCoroutine != null)
            {
                StopCoroutine(hitCoroutine);
            }

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
            if (hitCoroutine != null)
            {
                StopCoroutine(hitCoroutine);
                hitCoroutine = null;
                isHit = false;
                ResetFlash();
            }

            isDying = true;
            deathCompleteCallback = onComplete;
            deathCoroutine = StartCoroutine(DeathRoutine());
        }
```

Find `HitReactionRoutine`:

```csharp
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
```

Replace with (adds `DeathRoutine` immediately after):

```csharp
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
            for (int i = 0; i < totalToggles; i++)
            {
                spriteRenderer.enabled = !spriteRenderer.enabled;
                yield return new WaitForSeconds(deathBlinkInterval);
            }

            spriteRenderer.enabled = false;
            deathCoroutine = null;

            Action callback = deathCompleteCallback;
            deathCompleteCallback = null;
            callback?.Invoke();
        }
```

- [ ] **Step 3: Release a pending death callback on `OnDisable`, and skip pose/flash updates while dying**

Find, in `OnDisable()`:

```csharp
            if (hitCoroutine != null)
            {
                StopCoroutine(hitCoroutine);
                hitCoroutine = null;
                isHit = false;
                ResetFlash();
            }
        }
```

Replace with:

```csharp
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
```

Find, in `LateUpdate()`:

```csharp
        // LateUpdate so the mover's coroutine (which runs after Update) has
        // already written this frame's MoveProgress before we sample it.
        private void LateUpdate()
        {
            Vector3 baseOffset;
```

Replace with:

```csharp
        // LateUpdate so the mover's coroutine (which runs after Update) has
        // already written this frame's MoveProgress before we sample it.
        private void LateUpdate()
        {
            if (isDying)
            {
                return;
            }

            Vector3 baseOffset;
```

- [ ] **Step 4: Wire `EnemyController.Die()`**

In `Assets/Scripts/Runtime/Enemies/EnemyController.cs`, find:

```csharp
        /// <summary>
        /// Called by an attacker (e.g. PlayerController after a killing bump-attack)
        /// once this enemy's HP reaches 0. Unregisters from the turn resolver so it
        /// cannot act again, frees its tile immediately, then destroys the GameObject.
        /// The explicit ReleaseCell matters because Destroy is deferred to the end of
        /// the frame: relying on GridMover.OnDestroy alone would leave the corpse
        /// blocking its tile for the remainder of the turn it died on.
        /// </summary>
        public void Die()
        {
            resolver.UnregisterActor(this);
            mover.ReleaseCell();
            Destroy(gameObject);
        }
```

Replace with:

```csharp
        /// <summary>
        /// Called by an attacker (e.g. PlayerController after a killing bump-attack)
        /// once this enemy's HP reaches 0. Unregisters from the turn resolver and
        /// frees its tile immediately - a dying enemy must stop acting and stop
        /// blocking movement/attacks right away, exactly as before - but the
        /// GameObject's destruction is now deferred until its death-blink animation
        /// finishes (if a GridVisualAnimator is present), instead of destroying it
        /// the same frame. Falls back to immediate Destroy when no visual animator
        /// is present, matching every other animation fallback in this class.
        /// </summary>
        public void Die()
        {
            resolver.UnregisterActor(this);
            mover.ReleaseCell();

            if (visualAnimator != null)
            {
                visualAnimator.PlayDeath(() => Destroy(gameObject));
            }
            else
            {
                Destroy(gameObject);
            }
        }
```

- [ ] **Step 5: Verify it compiles, and confirm `EnemyControllerTests` is unaffected**

Use `mcp__UnityMCP__refresh_unity` then `mcp__UnityMCP__read_console` (errors only) — quote the actual output. Then run `mcp__UnityMCP__run_tests` (mode `EditMode`, filtered to `Jagara.Tests.EditMode.EnemyControllerTests`) and confirm all 5 tests still pass (none of them call `Die()`, so this is expected to require no test changes at all — quote the actual result to confirm).

- [ ] **Step 6: Commit**

```bash
git add "Assets/Scripts/Runtime/Gameplay/GridVisualAnimator.cs" "Assets/Scripts/Runtime/Enemies/EnemyController.cs"
git commit -m "Add enemy death-blink animation

- GridVisualAnimator.PlayDeath toggles sprite visibility on/off for a
  configurable number of cycles, then reports completion; cancels any
  in-flight hit-reaction first and freezes pose/flash updates while dying
- EnemyController.Die() defers Destroy(gameObject) to that completion
  callback while still freeing the turn slot and grid cell immediately"
```

---

### Task 3: Manual Play Mode verification

**Files:** none (verification only).

**Interfaces:** none produced — this task confirms Tasks 1-2 work together at runtime.

- [ ] **Step 1: Enter Play Mode**

Open `Assets/Scenes/DungeonPreview.unity` via `mcp__UnityMCP__manage_scene` if it isn't already open, then enter Play Mode via `mcp__UnityMCP__manage_editor`.

- [ ] **Step 2: Verify the white flash**

Locate a spawned `Enemy(Clone)` and its `GridVisualAnimator` (on its Visual child) via `mcp__UnityMCP__find_gameobjects`. Using `mcp__UnityMCP__execute_code`, either call `PlayHitReaction()` directly on it, or drive real combat (e.g. invoking `HealthState.TakeDamage` on its `EnemyController.Health` via reflection, as in the previous feature's verification) to trigger it through the real path. Confirm via a queried color/property-block read or a screenshot that the sprite visibly flashes toward white (not red, not a no-op) partway through the reaction, then fades back. Also confirm `EntityOutline`'s outline color is unaffected before, during, and after the flash (read `_OutlineColor` from the same `MaterialPropertyBlock` via `SpriteRenderer.GetPropertyBlock` through `execute_code`, or visually confirm the outline is still present in a screenshot) - this is the check that the read-modify-write pattern didn't clobber it.

- [ ] **Step 3: Verify the death animation**

Drive an enemy's `HealthState` down to 0 (directly via `TakeDamage`, or through a real attack), triggering `Die()`. Confirm:
- The enemy's sprite visibly blinks on/off rather than disappearing instantly.
- The enemy is removed from `TurnResolver`'s actor list and its grid cell is freed immediately (query `OccupancyGrid`/`TurnResolver` state via `execute_code`, or confirm the player can immediately step into/attack through the (visually still blinking) tile) — i.e. confirm the grid-state release did not wait for the blink to finish.
- The GameObject is actually destroyed once the blink sequence ends (poll instance validity via `execute_code` a moment after the expected ~0.6s blink duration).

- [ ] **Step 4: Verify the slower timing feels right**

Trigger a player attack against an adjacent enemy (directly invoking `PlayerController.PerformAttack` via reflection if input simulation isn't practical, as in the previous feature's verification) and confirm the lunge now takes noticeably longer (~0.24s) than before, and that a hit reaction lasts ~0.33s, without introducing any new stuck-input or stuck-turn-resolution symptoms (check `TurnResolver.IsResolving` returns to false once everything settles).

- [ ] **Step 5: Check the console for errors**

Use `mcp__UnityMCP__read_console` (errors only). Expected: no errors, including no shader warnings, throughout all of the above.

- [ ] **Step 6: Exit Play Mode**

Use `mcp__UnityMCP__manage_editor` to stop Play Mode. Confirm no scene/prefab/material edits persisted (`git status --short` clean) — everything above should have been runtime-only.

- [ ] **Step 7: Note any tuning follow-ups**

If the flash, blink timing, or new durations felt off, note the specific field(s) (`hitFlashColor`, `deathBlinkCount`, `deathBlinkInterval`, `attackDuration`, `hitDuration`) and suggested values as a follow-up — do not change the shipped defaults as part of this verification task.

---

## Self-Review Notes

- **Spec coverage:** the shader/flash mechanism (Task 1), the death animation (Task 2), and the timing slowdown (folded into Task 1, since it edits the same field-declaration block) all map directly to the three asks in the spec. Manual verification (Task 3) covers all three.
- **Placeholder scan:** no TBD/TODO markers; every step has literal, complete code.
- **Type consistency:** `GridVisualAnimator.PlayDeath(Action onComplete = null)` (Task 2) matches its one call site in `EnemyController.Die()` (Task 2, same task). `ResetFlash()` (introduced Task 1) is reused unchanged by `PlayDeath` (Task 2) with no signature change. No other public signature changes in this plan.
- **Scope check:** single cohesive change across two files' logic (shader+animator) plus one consumer (`EnemyController`) - appropriately sized for one plan, not split further.
