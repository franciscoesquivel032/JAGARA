# Hit-Impact Particle Burst — Design

Status: approved
Date: 2026-08-09

## Problem

Melee hits already have an attack lunge and a hit-reaction flash+shake (see
`2026-08-08-attack-and-hit-animations-design.md`), but no particle beat. This spec adds a white
radial spark burst at the moment a hit connects — a single shared "impact" effect on the entity
receiving the damage, not two separate attacker/dealt and defender/received effects. Every attack
has both an attacker and a defender, and the defender-side hook (`HealthState.OnHPChanged`) already
fires symmetrically for both directions (player hits enemy → enemy receives the burst; enemy hits
player → player receives the burst), so one hook covers "dealt" and "received" automatically.

## Existing architecture this builds on

- `HealthState.OnHPChanged` (`Assets/Scripts/Runtime/Resources/HealthState.cs`): fires only on
  genuine damage, from `TakeDamage`.
- `PlayerController.HandleHPChanged` (line ~357) and `EnemyController.HandleHPChanged` (line
  ~212): both already subscribe to `OnHPChanged` and call `visualAnimator?.PlayHitReaction()`
  purely for cosmetics. Neither file needs to change for this feature.
- `GridVisualAnimator.PlayHitReaction()` (`Assets/Scripts/Runtime/Gameplay/GridVisualAnimator.cs`,
  line 186): the single hook point already used for the flash+shake overlay. Purely cosmetic,
  never blocks or reports completion, restarting stops any in-flight reaction first.
- Particle precedent: `Assets/Prefabs/Player.prefab` has a `TorchFlame` child parented under the
  same `Visual` transform as `GridVisualAnimator`, holding a looping ambient `ParticleSystem`
  toggled via `TorchFlameToggle.cs`. That's a persistent-loop use case, different from the
  one-shot burst here, but it establishes the "particle child lives under `Visual`" structural
  convention this design mirrors. `Assets/Prefabs/Enemy.prefab` has no particle system today.

## Design

### 1. Prefab structure

Both `Assets/Prefabs/Player.prefab` and `Assets/Prefabs/Enemy.prefab` gain a `HitParticles` child
under `Visual` (sibling of `TorchFlame` on Player):

```
Visual (GridVisualAnimator)
├── TorchFlame        (Player only, pre-existing, unrelated to this feature)
└── HitParticles (new)
    └── ParticleSystem
```

`ParticleSystem` configured as a one-shot radial burst, not a loop:

- **Main**: `Looping = false`, `Play On Awake = false`, `Duration = 0.3`, `Start Lifetime ≈
  0.2–0.3` (random range), `Start Speed ≈ 1.5–2.5` (random range), `Start Size ≈ 0.03–0.06` (grid
  cell = 1 unit), `Start Color = white`, `Simulation Space = World` (so the burst stays put on the
  impact frame instead of getting dragged along by the entity's next grid hop), `Max Particles ≈ 20`.
- **Emission**: no rate-over-time; a single **Burst** at time `0`, count `10–15`.
- **Shape**: `Sphere`/`Circle`, small radius (~0.05) so particles originate at the impact point and
  radiate outward in random directions.
- **Color over Lifetime**: alpha `1.0 → 0.0` so particles fade rather than popping off abruptly.
- **Size over Lifetime** (polish): `1.0 → 0.6` shrink for a crisper spark read.
- **Renderer**: sorting layer matching the entity's sprite, sorting order above it (mirroring
  `TorchFlame`'s explicit sorting-order pattern) so the burst isn't occluded by the sprite it sits on.
- **Material**: `Assets/Art/Materials/HitSparkParticle.mat` — a new material using the same
  `Universal Render Pipeline/Particles/Unlit` shader and built-in soft-circle `Default-Particle`
  texture as `TorchEmberParticle.mat` (Transparent surface, Additive blend, white base color).
  A real material asset was required rather than relying on the ParticleSystem's built-in
  fallback material: an unconfigured URP particle renderer falls back to a placeholder with no
  valid shader assigned, which renders as solid pink/magenta (Unity's standard "missing shader"
  indicator) instead of the intended white burst.

Structural changes are made via MCP for Unity tools with the Editor in Edit Mode, never by
hand-editing prefab YAML, and never verified while in Play Mode (Play Mode changes don't persist).

### 2. `GridVisualAnimator.cs` — the only script touched

One new field in the existing `[Header("Hit reaction")]` block:

```csharp
[SerializeField] private ParticleSystem hitParticles;
```

Triggered inside `PlayHitReaction()` itself, so the burst fires the instant the hit registers
rather than waiting a frame for the coroutine to start:

```csharp
public void PlayHitReaction()
{
    if (hitCoroutine != null)
    {
        StopCoroutine(hitCoroutine);
    }

    hitParticles?.Play();
    hitCoroutine = StartCoroutine(HitReactionRoutine());
}
```

- Null-tolerant (`?.Play()`) — same "purely cosmetic, tolerate absence" convention already used
  for `visualAnimator`/`cameraShake` elsewhere in this codebase. No `Awake()` auto-lookup; the
  field is wired explicitly per prefab.
- No change to `HitReactionRoutine()`, `LateUpdate()`, or `OnDisable()` — the burst is
  non-looping and self-terminating, so it needs no cleanup and isn't gated by `isHit`.
- `PlayerController.cs` and `EnemyController.cs` require zero edits: both already call
  `visualAnimator?.PlayHitReaction()` from `HandleHPChanged`, which fires for both "player dealt
  damage to enemy" and "enemy dealt damage to player" — wiring the trigger once inside
  `GridVisualAnimator` covers both directions automatically.

### 3. Wiring the field on both prefabs

After the script compiles, each prefab's `Visual` GameObject has its `GridVisualAnimator`
component's `hitParticles` field set to reference the new `HitParticles` child, via MCP with the
Editor in Edit Mode, then saved.

## Out of scope

- No death-particle changes — `PlayDeath()` and its blink-vanish sequence are untouched.
- No separate attacker-side "swing" effect — explicitly declined in favor of the single shared
  defender-side burst.
- No ScriptableObject-based particle presets — a single burst profile lives directly on each
  prefab's `ParticleSystem` component; YAGNI, same precedent as the torch-flicker design's
  deferred SO presets until multiple "moods" are actually needed.
- No changes to `CombatResolver.cs`, `HealthState.cs`, or damage math — purely a visual layer
  downstream of already-resolved combat, same boundary the attack-and-hit-animations design established.

## Testing

No new pure-math logic exists to Edit-Mode-test (unlike the hit-flash/shake curves, which had
testable pure functions in `PlayerMotionAnimator`) — this is a MonoBehaviour/ParticleSystem
hookup, verified manually via MCP Play Mode observation:

1. Player attacks an adjacent enemy: confirm a white spark burst appears at the enemy's position
   at the moment of impact, alongside the existing flash/shake, fading within ~0.2–0.3s without looping.
2. An enemy attacks the player: confirm the same burst appears on the player.
3. Confirm the burst doesn't visually drag behind the entity on its next hop (validates
   `Simulation Space = World`) and doesn't idle-loop or replay on its own (validates
   `Play On Awake = false` + non-looping).
4. `read_console` shows no new runtime errors/warnings.

## Open questions / follow-ups (not blocking this design)

- Exact particle tuning (burst count, speed range, fade curve, whether the default particle
  material reads well enough or a bespoke soft-circle sprite is needed) is a visual judgment call
  to be finalized in the Editor once the effect exists — the numeric defaults above are starting
  points, not final, consistent with the torch-flicker design's precedent of deferring tuning.
