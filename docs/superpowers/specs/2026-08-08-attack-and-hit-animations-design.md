# Attack & Hit-Reaction Animations — Design

Status: approved
Date: 2026-08-08

## Problem

The existing bump-attack (`PlayerController.PerformAttack`, `EnemyController.PerformAttack`)
resolves damage and ends the turn in the same frame, with no visual beat for either the
attacker or the defender. This spec adds two procedural animations — an attack lunge and a
hit reaction — to both the player and enemies, reusing the project's existing procedural
animation architecture rather than introducing sprite-sheet or Animator-Controller-based
animation.

## Existing architecture this builds on

- `PlayerMotionAnimator` (`Assets/Scripts/Runtime/Gameplay/PlayerMotionAnimator.cs`): a
  static, pure-math class (Edit Mode testable) that computes procedural motion curves
  (idle breathing, move-hop height/squash, flip-X). Despite the name, it is already used
  by both the player and enemy visuals.
- `GridVisualAnimator`: a `MonoBehaviour` on each entity's Visual child that samples
  `PlayerMotionAnimator` every `LateUpdate` and writes `localPosition`/`localScale`/`flipX`.
  It never touches the root transform (that's `GridMover`'s job), so it can freely layer
  cosmetic offsets on top of grid movement.
- `TurnResolver.BeginActorAnimation()` / `EndActorAnimation()`: an animation-in-flight
  counter that holds `IsResolving` true. `EnemyController` already uses this to gate
  further player input on an enemy's move-step tween finishing.
- `HealthState.OnHPChanged` / `OnDeath`: already raised by `HealthState.TakeDamage`,
  currently only consumed by `HealthBarBinder` (and `PlayerController.HandleDeath` for
  `OnDeath`).

## Design

### 1. New pure math (`PlayerMotionAnimator`)

```csharp
private const float ShakeCycles = 3f;

// Bump-and-return lunge toward `direction` (a unit cardinal vector, as produced by
// CardinalDirectionResolver). Reuses ComputeHopHeight's triangular curve so an attack
// reads as a sibling of the move-hop rather than a different animation language.
public static Vector2 ComputeAttackLungeOffset(float progress, Vector2 direction, float lungeDistance)
    => direction * ComputeHopHeight(progress, lungeDistance);

// Decaying side-to-side shake for a hit reaction: fixed at three oscillations across
// the full duration, baked into the curve the same way ComputeHopHeight bakes its shape.
public static float ComputeHitShakeOffset(float progress, float magnitude)
{
    float p = Mathf.Clamp01(progress);
    return magnitude * Mathf.Sin(p * ShakeCycles * Mathf.PI * 2f) * (1f - p);
}

// Flash intensity for a hit reaction: 1 at the moment of impact, fading linearly to 0.
public static float ComputeHitFlashIntensity(float progress) => 1f - Mathf.Clamp01(progress);
```

All three are pure functions with no MonoBehaviour dependency, following the file's
existing convention, and get Edit Mode test coverage alongside the existing
`PlayerMotionAnimatorTests`.

### 2. `GridVisualAnimator` changes

New serialized tuning fields (defaults chosen for a snappy, readable but non-intrusive
feel; tunable per-prefab in the Inspector afterward):

- `attackLungeDistance` (0.35), `attackDuration` (0.16s)
- `hitShakeMagnitude` (0.08), `hitDuration` (0.22s), `hitFlashColor` (a warm red tint, `(1, 0.35, 0.35)`)

New public API:

```csharp
public void PlayAttack(Vector2Int direction, Action onComplete = null);
public void PlayHitReaction();
```

`PlayAttack` sets `flipX` toward `direction` immediately (reusing `ComputeFlipX`, same as
`HandleMoveStarted` does for movement), then runs a coroutine that becomes a third,
mutually-exclusive pose state in `LateUpdate` alongside idle/move-hop:

```
if (mover.IsMoving)      -> hop offset/scale (existing)
else if (isAttacking)    -> ComputeAttackLungeOffset / ComputeHopScale(attackProgress)
else                      -> idle breathing (existing)
```

`onComplete` fires once the lunge returns to rest — callers use this to know when it's
safe to proceed (see turn-timing below).

`PlayHitReaction` is an independent overlay, not a pose state: while active, `LateUpdate`
adds `ComputeHitShakeOffset` to whichever base offset is already active, and lerps
`SpriteRenderer.color` between its cached original color and `hitFlashColor` using
`ComputeHitFlashIntensity`. It never blocks or reports completion — purely cosmetic, per
the approved design, and safe to trigger regardless of what pose state the entity is in.

Restarting `PlayAttack`/`PlayHitReaction` while one is already running stops the previous
coroutine first (defensive; not expected to happen given one action per turn, but avoids
two coroutines fighting over the same state if it ever does).

### 3. `PlayerController` wiring

- Caches an optional `GridVisualAnimator` in `Awake()` (`GetComponentInChildren`, no error
  logged if absent — cosmetic, matching `EnemyController`'s existing tolerance for this
  component).
- `PerformAttack` gains the already-computed input `direction` as a parameter. Damage
  resolution (message posts, `enemy.Die()`) stays exactly as it is today, fully
  synchronous. What changes is turn-ending:
  - If `visualAnimator != null`: starts `PlayAttack(direction, HandleAttackAnimationCompleted)`
    where `HandleAttackAnimationCompleted` calls `turnResolver?.EndPlayerTurn()`. A new
    `attackAnimationInProgress` flag is set true until then.
  - If `visualAnimator == null`: calls `turnResolver?.EndPlayerTurn()` immediately, exactly
    as today — a deliberate fallback so nothing regresses where this component is absent.
- `Update()`'s early-out gate (`mover.IsMoving || turnResolver.IsResolving || inputGate.IsBlocked`)
  gains `|| attackAnimationInProgress`, so a held key cannot commit a second move/attack
  while the lunge is still playing.
- `attackAnimationInProgress` is reset to `false` in `Initialize()`, mirroring the existing
  `attackLatch.Clear()` reset, as defensive cleanup for a persistent player object.
- `BindStats()` additionally subscribes `stats.Health.OnHPChanged += HandleHPChanged`
  (`HandleHPChanged` calls `visualAnimator?.PlayHitReaction()`), unsubscribed in
  `OnDisable()` alongside the existing `OnDeath` unsubscription. This is additive and does
  not touch `HandleDeath`/defeat-dialogue flow.

### 4. `EnemyController` wiring

- `PerformAttack` computes `Vector2Int direction = context.PlayerCell - mover.CurrentCell`
  (guaranteed a unit cardinal vector, since this only runs when `IsAdjacent` is true).
  Damage resolution stays synchronous and unchanged.
  - If `visualAnimator != null`: calls `resolver.BeginActorAnimation()`, sets
    `animationPending = true`, and starts `PlayAttack(direction, ...)` with a completion
    callback that calls `resolver.EndActorAnimation()` and clears `animationPending` —
    reusing the exact `animationPending` flag and cleanup semantics `HandleMoveCompleted`
    and `OnDisable` already implement for move-step animations (extracted into one shared
    private method so both call sites agree).
  - If `visualAnimator == null`: no gating occurs at all — `TakeTurn()` returns exactly as
    it does today. This keeps `EnemyControllerTests` (which never wire up a
    `GridVisualAnimator`) passing unchanged.
- `Initialize()` additionally subscribes `health.OnHPChanged += HandleHPChanged` (calls
  `visualAnimator?.PlayHitReaction()`); unsubscribed in `OnDisable()`.

### Out of scope

- No death animation — a killing blow still destroys the enemy GameObject the same frame
  (`Die()`), which will cut short any in-flight hit-flash coroutine on that enemy. Flagged
  explicitly rather than silently left as a gap; a future task if desired.
- No new Animator Controllers or sprite-sheet animation — stays within the project's
  existing procedural-math animation system per current convention.
- No changes to damage math, `CombatResolver`, or `HealthState` — this is purely a visual
  layer on top of already-resolved combat.

## Testing

- New Edit Mode tests for `ComputeAttackLungeOffset`, `ComputeHitShakeOffset`,
  `ComputeHitFlashIntensity` in `PlayerMotionAnimatorTests.cs` (boundary behavior: zero at
  start/end for the lunge curve, decay-to-zero for shake and flash, direction scaling).
  `GridVisualAnimator`'s coroutine-driven behavior itself is MonoBehaviour/lifecycle-bound
  and is verified manually via MCP Play Mode observation instead, consistent with how
  `GridMover`'s tween is already handled.
- `EnemyControllerTests` must continue to pass unmodified — the design's null-`visualAnimator`
  fallback path is what guarantees this.
