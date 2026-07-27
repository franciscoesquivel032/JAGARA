# Entity & Environment Visual Contrast — Design

## Problem

In the Nightmare dungeon scene, the player, enemies, and floor decoration all render in similarly low-saturation, dark tones (see reference screenshot: a pink enemy blob, the player, a green/purple spiky enemy, and red root/vine decoration all sitting on dark cave tiles). Nothing is color-coded, so at a glance it's hard to tell what's a threat, what's the player, and what's inert environment dressing.

Two distinct sub-problems, addressed together:
1. Entities (player, enemies) don't stand out from the environment (floor, walls, decoration).
2. Player and enemies don't stand out from each other.

## Scope

- Applies only to the Nightmare scene context (dungeon rendering). The Hub currently has no enemies to distinguish, so it's out of scope.
- Faction distinction is **player vs. enemy only** — there is no ally/companion entity in the game yet (GDD mentions companions only as a possible future addition). No `Faction` enum or per-`EnemyConfigSO` color field is introduced; a third tier would be a follow-up if/when companions are added.
- Enemy sprites keep their individual designs/colors (Abomination, Eyeball, Leecher, Skull all look different); this design adds a uniform "this is an enemy" signal on top, it does not recolor enemies to match each other.

## Approach

Two complementary treatments, chosen together because they solve different halves of the problem:

1. **Faction outline** on entities (player = one color, enemy = another) — solves player-vs-enemy.
2. **Environment dimming** on floor/wall/decoration tiles — solves entities-vs-environment.

Two alternative single-pronged approaches were considered and rejected as insufficient on their own: outline-only (doesn't fix decoration blending into the background) and environment-dimming-only (doesn't help the player tell enemies apart from themselves). A ground-marker (colored disc under an entity's feet) was also considered as a third layer but deferred — outline + dimming already address both halves; the marker can be revisited later if it turns out entities still don't stand out enough underfoot.

## 1. Entity outline shader

- New shader: `Assets/Art/Shaders/EntityFactionOutline.shader` — URP 2D-compatible, using the standard pixel-art outline technique (sample neighboring texels' alpha; where the current texel's alpha is 0 but a neighbor's alpha isn't, draw a solid outline color instead). Same pass also applies a small saturation/brightness multiply to the sprite's own colors — this is the entity-side complement to the environment dimming.
- One shared `Material` asset instantiated from this shader (not per-entity — see below).
- New component: `EntityOutline` (`MonoBehaviour`, `[RequireComponent(typeof(SpriteRenderer))]`) with `[SerializeField] private Color outlineColor`. On `Awake`/`OnEnable`, it writes `outlineColor` into a `MaterialPropertyBlock` and applies it via `SpriteRenderer.SetPropertyBlock` — this avoids instantiating a unique `Material` per entity, which would break SRP batching.
- Colors are set directly on prefabs, not derived at runtime from any faction system:
  - `Player.prefab`: `EntityOutline` with `outlineColor = #E8B84B` (gold/amber — a neutral "this is you" signal, distinct from any threat color).
  - `Enemy.prefab`: `EntityOutline` with `outlineColor = #FF3B3B` (red — uniform across all enemy types regardless of `EnemyConfigSO`).
- `PlayerController` currently holds no `SpriteRenderer` reference (verified: it only drives `GridMover`). Adding `EntityOutline` to the player's sprite GameObject/child is what introduces that reference — scoped to this component, not added elsewhere.
- `EnemyController` already fetches `SpriteRenderer` via `GetComponentInChildren` (existing code) — `EntityOutline` sits alongside it on the same GameObject and does not change existing sprite-swap logic (`EnemyConfigSO.Sprite` assignment is untouched).

## 2. Environment dimming

- New field on `NightmareThemeSO`: `[SerializeField] private Color environmentTint`, alongside the existing `FogSettings` and enemy roster. Default value should be desaturated but not near-black (e.g. `#8C8C99`) so floor/wall tile detail stays legible — a pure-white default is a safe no-op (no dimming applied) if a theme asset doesn't set it.
- `FloorInstantiator` exposes two `Tilemap` references already: `tilemap` (floor/walls) and `decorationTilemap` (the root/vine clutter and similar). Both use Unity's built-in `TilemapRenderer.color`, which multiplies with each tile's own sprite color — no new shader needed for this half.
- `NightmareBootstrap` already holds references to both `floorInstantiator` and `nightmareTheme`, and already calls `fogController.Apply(nightmareTheme)` right after floor setup. This design adds one step in the same place: after floor instantiation, set `floorInstantiator.Tilemap.GetComponent<TilemapRenderer>().color` and the equivalent for `decorationTilemap` to `nightmareTheme.environmentTint`.
- Because decoration lives on its own `decorationTilemap` (confirmed via `FloorInstantiator.cs` — `PaintDecoration` writes into `decorationTilemap`, separate from the floor/wall `tilemap`), dimming both tilemaps automatically dims the red root/vine clutter with no per-decoration-tile special-casing.

## Data flow

```
NightmareBootstrap.Setup()
  → DungeonGenerator.Generate(seed, depthParams) → FloorData
  → FloorInstantiator instantiates tilemap + decorationTilemap from FloorData
  → NightmareBootstrap applies nightmareTheme.environmentTint to both TilemapRenderers   (NEW STEP — mirrors existing fogController.Apply(nightmareTheme) call)
  → fogController.Apply(nightmareTheme)   (existing, unchanged)
  → Player/Enemy prefabs spawned — each already carries its own EntityOutline component
     and color from the prefab; no runtime wiring needed at spawn time.
```

## Edge cases

- `environmentTint` left at pure white on a `NightmareThemeSO` asset that predates this change: `TilemapRenderer.color` multiply is a no-op, so existing theme assets render unchanged until someone deliberately sets a tint.
- Outline shader on a sprite with fully-transparent pixels (e.g. an enemy mid-fade-out) must not throw or draw stray outline artifacts — neighbor-sampling naturally produces no outline when there's no opaque texel nearby.
- `MaterialPropertyBlock` must be reapplied if a `SpriteRenderer`'s sprite changes at runtime (relevant for `EnemyController`, which swaps `.sprite` from `EnemyConfigSO`) — the property block is independent of which sprite is assigned, so no re-application is needed, but this is called out because it's the kind of thing that looks fine until a sprite swap silently loses the tint state (it won't here, since the block is set on the renderer, not tied to a specific sprite).

## Testing

This is rendering/visual work, bound to `MonoBehaviour` lifecycle (shader application, `SpriteRenderer`/`TilemapRenderer` state) — per project convention (`CLAUDE.md`) this is not Edit Mode Test territory (no pure-logic algorithm to assert against). Verification is manual:
- Play Mode / MCP-driven check that `Player.prefab` and `Enemy.prefab` render their respective outline colors correctly.
- Confirm `environmentTint` reaches both `tilemap` and `decorationTilemap` renderers without affecting entity sprites.
- Visual comparison against the original reference screenshot's scenario (enemy blob, player, spiky enemy, root decoration in frame together) to confirm the contrast problem is resolved.

## Out of scope / explicitly deferred

- Ally/companion faction color — no third tier exists yet; adding one is a follow-up once companions are implemented per the GDD.
- Ground/ floor color marker under entities — considered as a possible third visual layer, deferred unless outline + dimming turn out insufficient in practice.
- Any change to Hub scene rendering.
- Any change to enemy stat/ability data (`EnemyConfigSO` combat fields untouched).
