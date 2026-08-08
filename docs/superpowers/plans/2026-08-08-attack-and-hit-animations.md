# Attack & Hit-Reaction Animations Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a procedural attack-lunge animation (player and enemies) and a procedural hit-reaction flash+shake (player and enemies) to the existing bump-attack combat, without introducing sprite-sheet or Animator-Controller-based animation.

**Architecture:** Extends the project's existing procedural animation system: pure-math curves live in `PlayerMotionAnimator` (Edit Mode testable), sampled every `LateUpdate` by `GridVisualAnimator` on each entity's Visual child. The attack lunge becomes a third pose state (alongside idle/move-hop); the hit reaction is a cosmetic overlay on top of whichever pose is active. `PlayerController` and `EnemyController` each gain a `PerformAttack` that starts the lunge and defers turn-ending to its completion, using the existing `TurnResolver.BeginActorAnimation`/`EndActorAnimation` gate (enemies) or a new local flag (player).

**Tech Stack:** Unity 6000.5.3f1, C#, Unity Test Framework (Edit Mode), MCP for Unity (Play Mode verification).

## Global Constraints

- No Animator Controllers or sprite-sheet animation — stay within the existing procedural-math system (see `docs/superpowers/specs/2026-08-08-attack-and-hit-animations-design.md`).
- No changes to damage math, `CombatResolver`, or `HealthState` — this is a visual layer on top of already-resolved combat.
- `EnemyControllerTests` must continue to pass unmodified.
- No death animation (out of scope, flagged in the spec).
- `[SerializeField] private` for Inspector-exposed fields; never `GetComponent`/`Find` inside `Update()`; unsubscribe every C# event in `OnDisable()`.
- Full spec: `docs/superpowers/specs/2026-08-08-attack-and-hit-animations-design.md`.

---

### Task 1: Attack-lunge and hit-reaction math in `PlayerMotionAnimator`

**Files:**
- Modify: `Assets/Scripts/Runtime/Gameplay/PlayerMotionAnimator.cs`
- Test: `Assets/Tests/EditMode/PlayerMotionAnimatorTests.cs`

**Interfaces:**
- Consumes: `PlayerMotionAnimator.ComputeHopHeight(float progress, float hopHeight) -> float` (already exists in this file, lines 27-31).
- Produces (used by Task 2):
  - `PlayerMotionAnimator.ComputeAttackLungeOffset(float progress, Vector2 direction, float lungeDistance) -> Vector2`
  - `PlayerMotionAnimator.ComputeHitShakeOffset(float progress, float magnitude) -> float`
  - `PlayerMotionAnimator.ComputeHitFlashIntensity(float progress) -> float`

- [ ] **Step 1: Write the failing tests**

Open `Assets/Tests/EditMode/PlayerMotionAnimatorTests.cs`. Insert the following test methods immediately before the final two closing braces (after the existing `ComputeFlipX_MirrorsAgainstNativeFacing_KeepsFacingOnVertical` method, i.e. right after its closing `}` at line 136):

```csharp
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
```

- [ ] **Step 2: Run the tests to verify they fail to compile**

Use the `mcp__UnityMCP__run_tests` tool with mode `EditMode`, filtered to `Jagara.Tests.EditMode.PlayerMotionAnimatorTests`. Expected: compile error — `ComputeAttackLungeOffset`, `ComputeHitShakeOffset`, and `ComputeHitFlashIntensity` do not exist on `PlayerMotionAnimator`.

- [ ] **Step 3: Implement the three functions**

Open `Assets/Scripts/Runtime/Gameplay/PlayerMotionAnimator.cs`. Insert the following inside the class, immediately before its closing `}` (i.e. after `ComputeFlipX`, which ends at line 49):

```csharp

        // Shake frequency for ComputeHitShakeOffset, baked into the curve so
        // callers only supply progress/magnitude - same spirit as
        // ComputeHopHeight's fixed triangular shape.
        private const float ShakeCycles = 3f;

        // Bump-and-return lunge toward `direction` (a unit cardinal vector, as
        // produced by CardinalDirectionResolver). Reuses ComputeHopHeight's
        // triangular curve so an attack reads as a sibling of the move-hop
        // rather than a different animation language.
        public static Vector2 ComputeAttackLungeOffset(float progress, Vector2 direction, float lungeDistance)
        {
            return direction * ComputeHopHeight(progress, lungeDistance);
        }

        // Decaying side-to-side shake for a hit reaction: amplitude decays
        // linearly to 0 across ShakeCycles oscillations.
        public static float ComputeHitShakeOffset(float progress, float magnitude)
        {
            float p = Mathf.Clamp01(progress);
            return magnitude * Mathf.Sin(p * ShakeCycles * Mathf.PI * 2f) * (1f - p);
        }

        // Flash intensity for a hit reaction: 1 at the moment of impact,
        // fading linearly to 0 by the end of the reaction.
        public static float ComputeHitFlashIntensity(float progress)
        {
            return 1f - Mathf.Clamp01(progress);
        }
```

- [ ] **Step 4: Run the tests to verify they pass**

Use the `mcp__UnityMCP__run_tests` tool with mode `EditMode`, filtered to `Jagara.Tests.EditMode.PlayerMotionAnimatorTests`. Expected: all tests pass, including the pre-existing ones (no regressions).

- [ ] **Step 5: Commit**

```bash
git add "Assets/Scripts/Runtime/Gameplay/PlayerMotionAnimator.cs" "Assets/Tests/EditMode/PlayerMotionAnimatorTests.cs"
git commit -m "Add attack-lunge and hit-reaction math to PlayerMotionAnimator"
```

---

### Task 2: `GridVisualAnimator.PlayAttack` and `PlayHitReaction`

**Files:**
- Modify: `Assets/Scripts/Runtime/Gameplay/GridVisualAnimator.cs`

**Interfaces:**
- Consumes: the three functions from Task 1, plus existing `PlayerMotionAnimator.ComputeFlipX`, `ComputeHopScale`, `ComputeIdleBreathScale`, `ComputeGroundedOffset`, `ComputeHopHeight`.
- Produces (used by Tasks 3 and 4):
  - `public void PlayAttack(Vector2Int direction, Action onComplete = null)` — plays a bump-and-return lunge toward `direction`, calling `onComplete` once it returns to rest.
  - `public void PlayHitReaction()` — plays a flash+shake overlay; fire-and-forget, no completion callback.

This class is MonoBehaviour/coroutine-lifecycle-bound (same category as `GridMover`'s tween — see `GridMoverTests.cs`'s header comment and CLAUDE.md's Testing section), so per existing project convention it is verified through manual Play Mode observation (Task 5) rather than an Edit Mode test.

- [ ] **Step 1: Replace the file contents**

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
    /// localPosition/localScale/color, so it never fights GridMover, which
    /// tweens the root. Math lives in PlayerMotionAnimator (Edit Mode
    /// testable).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class GridVisualAnimator : MonoBehaviour
    {
        [SerializeField] private GridMover mover;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private float idleBreathAmount = 0.03125f;
        [SerializeField] private float idleBreathSpeed = 2.5f;
        [SerializeField] private float hopHeight = 0.125f;
        [SerializeField] private float squashAmount = 0.06f;
        [SerializeField] private bool spriteFacesLeft = true;

        [Header("Attack lunge")]
        [SerializeField] private float attackLungeDistance = 0.35f;
        [SerializeField] private float attackDuration = 0.16f;

        [Header("Hit reaction")]
        [SerializeField] private float hitShakeMagnitude = 0.08f;
        [SerializeField] private float hitDuration = 0.22f;
        [SerializeField] private Color hitFlashColor = Color.white;

        private Vector3 baseLocalPosition;
        private float spriteHeight;
        private Color originalColor;

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
            originalColor = spriteRenderer.color;
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
                attackCompleteCallback = null;
            }

            if (hitCoroutine != null)
            {
                StopCoroutine(hitCoroutine);
                hitCoroutine = null;
                isHit = false;
                spriteRenderer.color = originalColor;
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
            spriteRenderer.color = originalColor;
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
                spriteRenderer.color = Color.Lerp(originalColor, hitFlashColor, PlayerMotionAnimator.ComputeHitFlashIntensity(hitProgress));
            }

            transform.localPosition = baseLocalPosition + baseOffset + hitOffset;
            transform.localScale = new Vector3(scale.x, scale.y, 1f);
        }
    }
}
```

Note: `attackDirection` (a `Vector2Int`) is passed directly where `ComputeAttackLungeOffset` expects a `Vector2` — this compiles via Unity's built-in implicit `Vector2Int` → `Vector2` conversion, matching how the rest of the codebase already treats cardinal directions.

- [ ] **Step 2: Verify it compiles with no console errors**

Use `mcp__UnityMCP__refresh_unity` to trigger a recompile, then `mcp__UnityMCP__read_console` (errors only) to confirm there are no compile errors from this change.

- [ ] **Step 3: Commit**

```bash
git add "Assets/Scripts/Runtime/Gameplay/GridVisualAnimator.cs"
git commit -m "Add attack-lunge and hit-reaction playback to GridVisualAnimator"
```

---

### Task 3: Wire the player's attack lunge and hit reaction

**Files:**
- Modify: `Assets/Scripts/Runtime/Gameplay/PlayerController.cs`

**Interfaces:**
- Consumes: `GridVisualAnimator.PlayAttack(Vector2Int, Action)`, `GridVisualAnimator.PlayHitReaction()` (Task 2); `HealthState.OnHPChanged` (event, signature `Action<int,int>`, already defined in `Assets/Scripts/Runtime/Resources/HealthState.cs`).
- Produces: no public API changes — `PerformAttack` becomes `private void PerformAttack(EnemyController enemy, Vector2Int direction)` (was `PerformAttack(EnemyController enemy)`), used only internally by `Update()`.

- [ ] **Step 1: Cache the visual animator and add the animation-gating flag**

In `Assets/Scripts/Runtime/Gameplay/PlayerController.cs`, find this field block (around line 49-57):

```csharp
        private HealthBarBinder healthBarBinder;
        private ParanoiaBarBinder paranoiaBarBinder;

        private InputAction moveAction;
```

Replace it with:

```csharp
        private HealthBarBinder healthBarBinder;
        private ParanoiaBarBinder paranoiaBarBinder;
        private GridVisualAnimator visualAnimator;

        private InputAction moveAction;

        // True from PerformAttack starting the lunge until its completion
        // callback fires. Joins mover.IsMoving in Update's gate so a held
        // key can't commit a second action while the lunge is still playing.
        private bool attackAnimationInProgress;
```

Then find, in `Awake()` (around line 80-81):

```csharp
            healthBarBinder = GetComponentInChildren<HealthBarBinder>();
            paranoiaBarBinder = GetComponentInChildren<ParanoiaBarBinder>();
```

Replace with:

```csharp
            healthBarBinder = GetComponentInChildren<HealthBarBinder>();
            paranoiaBarBinder = GetComponentInChildren<ParanoiaBarBinder>();
            visualAnimator = GetComponentInChildren<GridVisualAnimator>();
```

No `Debug.LogError` for a missing `visualAnimator` — it's cosmetic-only, matching how `EnemyController` already treats this same component.

- [ ] **Step 2: Reset the flag on (re)initialize**

Find, in `Initialize()` (around line 141):

```csharp
            attackLatch.Clear();
            deathPending = false;
```

Replace with:

```csharp
            attackLatch.Clear();
            attackAnimationInProgress = false;
            deathPending = false;
```

- [ ] **Step 3: Gate `Update()` on the new flag and pass direction to `PerformAttack`**

Find (around line 190):

```csharp
            if (mover.IsMoving || (turnResolver != null && turnResolver.IsResolving) || (inputGate != null && inputGate.IsBlocked))
            {
                return;
            }
```

Replace with:

```csharp
            if (mover.IsMoving || attackAnimationInProgress || (turnResolver != null && turnResolver.IsResolving) || (inputGate != null && inputGate.IsBlocked))
            {
                return;
            }
```

Then find (around line 201-209):

```csharp
            Vector2Int target = mover.CurrentCell + direction;
            if (TryGetAttackTarget(target, out EnemyController enemy))
            {
                // Attacking and moving are separate actions: latching this direction
                // is what stops the same held key from also walking the player into
                // the tile once the enemy dies and vacates it.
                attackLatch.Latch(direction);
                PerformAttack(enemy);
                return;
            }
```

Replace with:

```csharp
            Vector2Int target = mover.CurrentCell + direction;
            if (TryGetAttackTarget(target, out EnemyController enemy))
            {
                // Attacking and moving are separate actions: latching this direction
                // is what stops the same held key from also walking the player into
                // the tile once the enemy dies and vacates it.
                attackLatch.Latch(direction);
                PerformAttack(enemy, direction);
                return;
            }
```

- [ ] **Step 4: Update `PerformAttack` to play the lunge and defer turn-ending**

Find the full `PerformAttack` method and its doc comment (around lines 233-258):

```csharp
        /// <summary>
        /// Resolves a basic bump-attack (no PP cost) against an adjacent enemy
        /// instead of moving into its tile. Ends the turn directly since there's
        /// no move tween to wait for (unlike HandleMoveCompleted's movement path).
        /// The damage line is posted on a killing blow too, not just a surviving
        /// hit - otherwise the one attack whose number matters most is the one the
        /// log never shows.
        /// </summary>
        private void PerformAttack(EnemyController enemy)
        {
            if (stats == null)
            {
                return;
            }

            CombatResolver.AttackResult result = CombatResolver.ResolveBumpAttack(stats.AttackDamage, enemy.Health);
            Post(attackMessage, StyledName.Enemy(enemy.DisplayName), result.Damage);

            if (result.DefenderDied)
            {
                Post(enemyDefeatedMessage, StyledName.Enemy(enemy.DisplayName));
                enemy.Die();
            }

            turnResolver?.EndPlayerTurn();
        }
```

Replace with:

```csharp
        /// <summary>
        /// Resolves a basic bump-attack (no PP cost) against an adjacent enemy
        /// instead of moving into its tile. Damage resolves synchronously, but
        /// the turn now ends via HandleAttackAnimationCompleted once the attack
        /// lunge (see GridVisualAnimator.PlayAttack) returns to rest - unless no
        /// visual animator is present, in which case the turn ends immediately
        /// as before. The damage line is posted on a killing blow too, not just
        /// a surviving hit - otherwise the one attack whose number matters most
        /// is the one the log never shows.
        /// </summary>
        private void PerformAttack(EnemyController enemy, Vector2Int direction)
        {
            if (stats == null)
            {
                return;
            }

            CombatResolver.AttackResult result = CombatResolver.ResolveBumpAttack(stats.AttackDamage, enemy.Health);
            Post(attackMessage, StyledName.Enemy(enemy.DisplayName), result.Damage);

            if (result.DefenderDied)
            {
                Post(enemyDefeatedMessage, StyledName.Enemy(enemy.DisplayName));
                enemy.Die();
            }

            if (visualAnimator != null)
            {
                attackAnimationInProgress = true;
                visualAnimator.PlayAttack(direction, HandleAttackAnimationCompleted);
            }
            else
            {
                turnResolver?.EndPlayerTurn();
            }
        }

        /// <summary>
        /// Fires once the player's attack lunge returns to rest. The turn only
        /// ends here, not synchronously inside PerformAttack, so a held key
        /// can't commit a second action while the lunge is still playing (see
        /// attackAnimationInProgress in Update).
        /// </summary>
        private void HandleAttackAnimationCompleted()
        {
            attackAnimationInProgress = false;
            turnResolver?.EndPlayerTurn();
        }
```

- [ ] **Step 5: Wire the hit-reaction trigger**

Find, in `BindStats()` (around line 176):

```csharp
            stats.Health.OnDeath += HandleDeath;
            healthBarBinder?.Bind(stats.Health);
            paranoiaBarBinder?.Bind(stats.Paranoia);
```

Replace with:

```csharp
            stats.Health.OnDeath += HandleDeath;
            stats.Health.OnHPChanged += HandleHPChanged;
            healthBarBinder?.Bind(stats.Health);
            paranoiaBarBinder?.Bind(stats.Paranoia);
```

Find, in `OnDisable()` (around line 120-125):

```csharp
            if (stats != null && stats.Health != null)
            {
                stats.Health.OnDeath -= HandleDeath;
                healthBarBinder?.Unbind();
                paranoiaBarBinder?.Unbind();
            }
```

Replace with:

```csharp
            if (stats != null && stats.Health != null)
            {
                stats.Health.OnDeath -= HandleDeath;
                stats.Health.OnHPChanged -= HandleHPChanged;
                healthBarBinder?.Unbind();
                paranoiaBarBinder?.Unbind();
            }
```

Then add a new private method near `HandleDeath` (right after `HandleDeath`'s closing brace, around line 320):

```csharp
        /// <summary>
        /// Plays the player's hit-reaction flash+shake whenever HP actually
        /// drops (HealthState.OnHPChanged only fires on a genuine hit - see
        /// HealthState.TakeDamage). Purely cosmetic: does not gate input or
        /// interact with deathPending/HandleDeath in any way.
        /// </summary>
        private void HandleHPChanged(int current, int max)
        {
            visualAnimator?.PlayHitReaction();
        }
```

- [ ] **Step 6: Verify it compiles with no console errors**

Use `mcp__UnityMCP__refresh_unity` to trigger a recompile, then `mcp__UnityMCP__read_console` (errors only) to confirm there are no compile errors.

- [ ] **Step 7: Commit**

```bash
git add "Assets/Scripts/Runtime/Gameplay/PlayerController.cs"
git commit -m "Play attack-lunge and hit-reaction animations from PlayerController"
```

---

### Task 4: Wire the enemy's attack lunge and hit reaction

**Files:**
- Modify: `Assets/Scripts/Runtime/Enemies/EnemyController.cs`
- Test (verification only, no new tests): `Assets/Tests/EditMode/EnemyControllerTests.cs`

**Interfaces:**
- Consumes: `GridVisualAnimator.PlayAttack(Vector2Int, Action)`, `GridVisualAnimator.PlayHitReaction()` (Task 2); `TurnResolver.BeginActorAnimation()`/`EndActorAnimation()` (already exist in `Assets/Scripts/Runtime/TurnSystem/TurnResolver.cs`); `EnemyAIContext.PlayerCell` (already exists).
- Produces: no public API changes.

`EnemyControllerTests` never wires up a `GridVisualAnimator` on its test enemy GameObject (only `mover` and `spriteRenderer` are injected via reflection — see `SpawnEnemy` in the test file), so `visualAnimator` stays `null` there. The `if (visualAnimator != null)` guard below is what keeps those tests passing unmodified: with no visual animator, `PerformAttack` applies damage exactly as it does today and returns without touching `resolver.BeginActorAnimation()`.

- [ ] **Step 1: Rename `HandleMoveCompleted` to the shared `HandleAnimationCompleted`**

This lets the same completion handler be reused for both a move-step tween finishing and an attack lunge finishing. Find, in `OnEnable()`/`OnDisable()` (around lines 58-65):

```csharp
        private void OnEnable()
        {
            mover.OnMoveCompleted += HandleMoveCompleted;
        }

        private void OnDisable()
        {
            mover.OnMoveCompleted -= HandleMoveCompleted;

            if (resolver == null)
            {
                // Initialize was never called (e.g. disabled before wiring completed).
                return;
            }

            if (animationPending)
            {
                resolver.EndActorAnimation();
                animationPending = false;
            }

            resolver.UnregisterActor(this);
        }
```

Replace with:

```csharp
        private void OnEnable()
        {
            mover.OnMoveCompleted += HandleAnimationCompleted;
        }

        private void OnDisable()
        {
            mover.OnMoveCompleted -= HandleAnimationCompleted;

            if (health != null)
            {
                health.OnHPChanged -= HandleHPChanged;
            }

            if (resolver == null)
            {
                // Initialize was never called (e.g. disabled before wiring completed).
                return;
            }

            if (animationPending)
            {
                resolver.EndActorAnimation();
                animationPending = false;
            }

            resolver.UnregisterActor(this);
        }
```

Find the method itself (around lines 151-160):

```csharp
        private void HandleMoveCompleted()
        {
            if (!animationPending)
            {
                return;
            }

            resolver.EndActorAnimation();
            animationPending = false;
        }
```

Replace with:

```csharp
        private void HandleAnimationCompleted()
        {
            if (!animationPending)
            {
                return;
            }

            resolver.EndActorAnimation();
            animationPending = false;
        }
```

- [ ] **Step 2: Subscribe to the enemy's own `OnHPChanged` in `Initialize`**

Find, in `Initialize()` (around lines 87-93):

```csharp
        public void Initialize(EnemyConfigSO config, FloorData floor, Tilemap tilemap, Vector2Int startCell, TurnResolver resolver, EnemyAIContext context)
        {
            this.config = config;
            this.context = context;
            this.resolver = resolver;
            state = EnemyAIState.Dormant;
            health = new HealthState(config.BaseMaxHP);
```

Replace with:

```csharp
        public void Initialize(EnemyConfigSO config, FloorData floor, Tilemap tilemap, Vector2Int startCell, TurnResolver resolver, EnemyAIContext context)
        {
            this.config = config;
            this.context = context;
            this.resolver = resolver;
            state = EnemyAIState.Dormant;
            health = new HealthState(config.BaseMaxHP);
            health.OnHPChanged += HandleHPChanged;
```

- [ ] **Step 3: Play the attack lunge and gate the turn on it**

Find `PerformAttack` and its doc comment (around lines 162-176):

```csharp
        /// <summary>
        /// Resolves a basic bump-attack against the player's HealthState and
        /// records it against PlayerStatsSO's turn accumulator rather than
        /// posting a message directly - multiple enemies can be adjacent and
        /// attack in the same player turn, and PlayerController posts one
        /// combined message once the turn is fully resolved (see
        /// PlayerController.HandleTurnEnded). The player's own death handling
        /// (message, input freeze) is owned by PlayerController via its
        /// HealthState.OnDeath subscription, not here.
        /// </summary>
        private void PerformAttack()
        {
            CombatResolver.AttackResult result = CombatResolver.ResolveBumpAttack(config.BaseAttackDamage, context.PlayerStats.Health);
            context.PlayerStats.RecordIncomingAttack(config.DisplayName, result.Damage);
        }
```

Replace with:

```csharp
        /// <summary>
        /// Resolves a basic bump-attack against the player's HealthState and
        /// records it against PlayerStatsSO's turn accumulator rather than
        /// posting a message directly - multiple enemies can be adjacent and
        /// attack in the same player turn, and PlayerController posts one
        /// combined message once the turn is fully resolved (see
        /// PlayerController.HandleTurnEnded). The player's own death handling
        /// (message, input freeze) is owned by PlayerController via its
        /// HealthState.OnDeath subscription, not here. Also kicks off this
        /// enemy's attack-lunge animation (if a GridVisualAnimator is present),
        /// gating turn resolution on it exactly like a move step does via
        /// BeginActorAnimation/EndActorAnimation - if no visual animator is
        /// present (as in EnemyControllerTests), no gating happens at all and
        /// TakeTurn returns exactly as it does today.
        /// </summary>
        private void PerformAttack()
        {
            CombatResolver.AttackResult result = CombatResolver.ResolveBumpAttack(config.BaseAttackDamage, context.PlayerStats.Health);
            context.PlayerStats.RecordIncomingAttack(config.DisplayName, result.Damage);

            if (visualAnimator != null)
            {
                Vector2Int direction = context.PlayerCell - mover.CurrentCell;
                resolver.BeginActorAnimation();
                animationPending = true;
                visualAnimator.PlayAttack(direction, HandleAnimationCompleted);
            }
        }
```

- [ ] **Step 4: Add the hit-reaction handler**

Add a new private method near `PerformAttack` (right after its closing brace):

```csharp
        /// <summary>
        /// Plays this enemy's hit-reaction flash+shake whenever the player
        /// damages it (HealthState.OnHPChanged only fires on a genuine hit -
        /// see HealthState.TakeDamage). Purely cosmetic. On a killing blow,
        /// this coroutine starts but is immediately cut short by Die()
        /// destroying the GameObject in the same frame - a known, accepted
        /// gap (no death animation; see the design spec).
        /// </summary>
        private void HandleHPChanged(int current, int max)
        {
            visualAnimator?.PlayHitReaction();
        }
```

- [ ] **Step 5: Run `EnemyControllerTests` to confirm no regression**

Use the `mcp__UnityMCP__run_tests` tool with mode `EditMode`, filtered to `Jagara.Tests.EditMode.EnemyControllerTests`. Expected: all four existing tests still pass unmodified (`TakeTurn_PlayerFarAway_StaysDormantAndDoesNotMove`, `TakeTurn_PlayerWithinDetectionRange_WakesAndStepsToward`, `TakeTurn_AdjacentToPlayer_DoesNotMoveEvenThoughChasing`, `TakeTurn_ChasingEnemyLosesPlayerBeyondForgetRange_ReturnsToDormant`, `Initialize_SeedsHealthFromConfigBaseMaxHP`).

- [ ] **Step 6: Commit**

```bash
git add "Assets/Scripts/Runtime/Enemies/EnemyController.cs"
git commit -m "Play attack-lunge and hit-reaction animations from EnemyController"
```

---

### Task 5: Manual Play Mode verification

**Files:** none (verification only).

**Interfaces:** none produced — this task confirms Tasks 1-4 work together at runtime.

- [ ] **Step 1: Enter Play Mode**

Open `Assets/Scenes/DungeonPreview.unity` (the working nightmare scene, driven by `NightmareBootstrap`) via `mcp__UnityMCP__manage_scene`, then use `mcp__UnityMCP__manage_editor` to enter Play Mode.

- [ ] **Step 2: Trigger a player attack against an enemy and observe**

Drive the player (via the Input System actions the scene already wires up, or by directly invoking movement in the running scene through MCP) into an enemy to trigger a bump-attack. Confirm visually (screenshot or `manage_editor` state query):
- The player's Visual child lunges toward the enemy's tile and springs back.
- The enemy's Visual child flashes white and shakes at the moment of impact.
- No new keys can be committed until the lunge finishes (holding the attack direction does not commit a second attack mid-lunge).

- [ ] **Step 3: Trigger an enemy attack against the player and observe**

Let a woken/chasing enemy close to adjacency and bump-attack the player on its turn. Confirm:
- The enemy's Visual child lunges toward the player and springs back.
- The player's Visual child flashes and shakes at the moment of impact.
- Player input stays blocked (per `TurnResolver.IsResolving`) until the enemy's lunge finishes.

- [ ] **Step 4: Check the console for errors**

Use `mcp__UnityMCP__read_console` (errors only). Expected: no errors or exceptions from either animation path.

- [ ] **Step 5: Exit Play Mode**

Use `mcp__UnityMCP__manage_editor` to stop Play Mode. Per CLAUDE.md, do not treat anything observed only in Play Mode as a persisted structural change — no prefab/scene edits should have occurred during this task.

- [ ] **Step 6: Note any tuning follow-ups**

If the lunge/shake felt too subtle or too strong during observation, note the specific `GridVisualAnimator` field(s) (`attackLungeDistance`, `attackDuration`, `hitShakeMagnitude`, `hitDuration`, `hitFlashColor`) and suggested values as a follow-up — do not silently change the shipped defaults from Task 2 as part of this verification task.

---

## Self-Review Notes

- **Spec coverage:** every section of the design spec maps to a task — pure math (Task 1), `GridVisualAnimator` playback (Task 2), player wiring (Task 3), enemy wiring (Task 4), out-of-scope items (no death animation, no `CombatResolver`/`HealthState` changes) are respected by construction (no task touches those files). Manual verification (Task 5) covers the spec's stated Play Mode testing approach.
- **Placeholder scan:** no TBD/TODO markers; every step has literal, complete code.
- **Type consistency:** `ComputeAttackLungeOffset(float, Vector2, float) -> Vector2`, `ComputeHitShakeOffset(float, float) -> float`, `ComputeHitFlashIntensity(float) -> float` (Task 1) match their call sites in Task 2's `LateUpdate` exactly. `GridVisualAnimator.PlayAttack(Vector2Int, Action)` (Task 2) matches both call sites in Tasks 3 and 4. `PerformAttack(EnemyController, Vector2Int)`'s signature change in Task 3 is updated at its one call site in the same task's Step 3.
