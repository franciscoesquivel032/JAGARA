# Death Animation, True White Hit-Flash, and Slower Timing — Design

Status: approved
Date: 2026-08-08

## Problem

Follow-up to `2026-08-08-attack-and-hit-animations-design.md`. Three changes requested:

1. The hit-reaction "flash" currently tints `SpriteRenderer.color` toward a color, which is a
   multiply blend — it can darken/tint but can never brighten a sprite to white. The final
   review on the previous feature caught this and worked around it by defaulting the tint to
   red. The actual ask is a genuine white blink, which needs a different mechanism.
2. Enemies have no death animation — `EnemyController.Die()` destroys the GameObject the
   instant a killing blow lands, with no visual beat (this was an explicitly flagged gap in
   the previous design).
3. The attack lunge and hit reaction both read as slightly too fast; both durations increase
   by ~1.5x.

## Existing architecture this builds on

- `GridVisualAnimator` (`Assets/Scripts/Runtime/Gameplay/GridVisualAnimator.cs`): owns all
  procedural animation on an entity's Visual child — idle breathing, move-hop, attack lunge,
  hit-reaction shake. This change adds a death-blink routine and swaps the flash mechanism.
- `EntityOutline` (`Assets/Scripts/Runtime/Gameplay/EntityOutline.cs`) +
  `EntityFactionOutline.shader` (`Assets/Art/Shaders/EntityFactionOutline.shader`): the
  existing precedent for a per-instance shader effect driven without material instancing —
  `EntityOutline.Awake()` writes `_OutlineColor` into a `MaterialPropertyBlock` on the same
  `SpriteRenderer` that `GridVisualAnimator` lives on (both components sit on the same
  "Visual" child GameObject). This change follows the identical pattern for a new
  `_FlashAmount`/`_FlashColor` pair, sharing the same underlying property block.
- `EnemyController.Die()`: currently synchronous — `UnregisterActor`, `ReleaseCell`,
  `Destroy(gameObject)`, all in one call, all in one frame.

## Design

### 1. Shader: true white flash via `_FlashAmount`

`Assets/Art/Shaders/EntityFactionOutline.shader` gains two properties:

```hlsl
_FlashColor ("Flash Color", Color) = (1, 1, 1, 1)
_FlashAmount ("Flash Amount", Range(0, 1)) = 0
```

declared in the `Properties` block and mirrored in the `CBUFFER_START(UnityPerMaterial)` block
alongside the existing `_OutlineColor`/`_OutlineWidth`/`_OutlineIntensity`. In `frag()`, after
computing `baseSample` for the opaque (non-outline) path:

```hlsl
if (baseSample.a > 0.001)
{
    baseSample.rgb = lerp(baseSample.rgb, _FlashColor.rgb, _FlashAmount);
    return baseSample;
}
```

This is a genuine override blend (lerp toward the flash color), not a multiply, so
`_FlashAmount = 1` with `_FlashColor = white` renders solid white regardless of the sprite's
own texture/tint — the thing a `SpriteRenderer.color` multiply could never do. The outline
pass (the `else` branch, drawn only on the silhouette edge) is left untouched — only the
sprite body flashes.

### 2. `EntityOutline` / `GridVisualAnimator` share one `MaterialPropertyBlock`

`SpriteRenderer.SetPropertyBlock` replaces the block wholesale, so a caller must read the
renderer's *current* block before modifying it, or it will wipe out whatever the other
component already set. `EntityOutline.Awake()` already writes `_OutlineColor` once at scene
start and never touches the block again, so as long as `GridVisualAnimator` always does
`GetPropertyBlock` → mutate → `SetPropertyBlock` (never constructs a block and sets it
unconditionally), the two compose safely regardless of `Awake()` ordering between the two
components — `GridVisualAnimator` doesn't touch the flash property until `PlayHitReaction()`
is called at runtime, well after both components' `Awake()` have already run.

`GridVisualAnimator` adds:

```csharp
private static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");
private static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");
private MaterialPropertyBlock propertyBlock;
```

`propertyBlock` is constructed once in `Awake()`. Setting the flash amount becomes:

```csharp
spriteRenderer.GetPropertyBlock(propertyBlock);
propertyBlock.SetColor(FlashColorId, hitFlashColor);
propertyBlock.SetFloat(FlashAmountId, PlayerMotionAnimator.ComputeHitFlashIntensity(hitProgress));
spriteRenderer.SetPropertyBlock(propertyBlock);
```

called from `LateUpdate` while `isHit` is true, and once more with `_FlashAmount = 0` when the
hit-reaction coroutine ends (interrupted or completed) so no stale flash persists. This
replaces the current `spriteRenderer.color = Color.Lerp(originalColor, hitFlashColor, ...)`
line entirely. `originalColor` and every read of `spriteRenderer.color` for flash purposes are
removed — `SpriteRenderer.color` is no longer touched by this class at all.

`hitFlashColor`'s default reverts to `Color.white` (previously changed to a red tint as a
workaround for the multiply-blend limitation; the shader fix removes the reason for that
workaround).

### 3. Death animation: `GridVisualAnimator.PlayDeath`

New serialized fields:

```csharp
[Header("Death")]
[SerializeField] private int deathBlinkCount = 5;
[SerializeField] private float deathBlinkInterval = 0.06f;
```

New state and public method:

```csharp
private bool isDying;
private Coroutine deathCoroutine;

public void PlayDeath(Action onComplete = null)
```

`PlayDeath`:
- Cancels any in-flight hit-reaction first (a killing blow already fired `OnHPChanged` →
  `PlayHitReaction` before `OnDeath` fires this call) — stops `hitCoroutine` if running, clears
  `isHit`, and zeroes `_FlashAmount` via the property block immediately, so the blink isn't
  fighting a stale flash update in the same frame.
- Sets `isDying = true`.
- Starts a coroutine that toggles `spriteRenderer.enabled = !spriteRenderer.enabled` every
  `deathBlinkInterval` seconds, for `deathBlinkCount * 2` toggles (on→off counts as one blink
  cycle), then explicitly sets `spriteRenderer.enabled = false` (so parity never leaves it
  visible) and invokes `onComplete`.

`LateUpdate` gains an early return when `isDying` is true — skipping all pose/idle-breathing
math and the hit-flash property-block update, since a dying corpse doesn't need to keep
breathing or reacting while it flickers out.

This is purely cosmetic and reports completion (unlike `PlayHitReaction`) only because its
caller (`EnemyController.Die()`) needs to know when it's safe to actually destroy the
GameObject — it does not gate `TurnResolver`/turn-ending in any way, the same way the previous
design kept hit-reactions off the turn-timing critical path.

### 4. `EnemyController.Die()` wiring

```csharp
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

`UnregisterActor`/`ReleaseCell` stay immediate and unconditional — a dying enemy must stop
acting and free its tile for movement/attacks right away, exactly as today; only the
GameObject's actual destruction is deferred, and only when a visual animator is present to
animate the delay. No `EnemyControllerTests` test calls `Die()`, so this doesn't affect that
suite either way.

### 5. Timing changes

In `GridVisualAnimator`:
- `attackDuration`: `0.16f` → `0.24f`
- `hitDuration`: `0.22f` → `0.33f`

### Out of scope

- No player death animation — the player's death is a narrative beat (Voice dialogue), not a
  destroy/respawn, and is unaffected by this change.
- No changes to `CombatResolver`, `HealthState`, or damage math.
- No shader changes beyond the two new flash properties — the outline pass, blending mode, and
  everything else in `EntityFactionOutline.shader` stays as-is.

## Testing

- No new pure-math functions are introduced — the death blink is a discrete on/off timer loop,
  not a continuous eased curve, so (consistent with `GridMover`'s tween, which also has no
  extracted pure-math test) it doesn't need one.
- The shader change and the property-block coordination with `EntityOutline` are not
  Edit-Mode-testable (no shader test infra in this project) — verified via manual Play Mode
  observation through MCP, same approach as the previous feature's Task 5.
- `EnemyControllerTests` must continue to pass unmodified (no test calls `Die()` today, so this
  is expected to require no changes to that file at all — confirmed during implementation).
