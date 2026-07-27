# Decoration Tile Layer + Weighted Wall Fill Variants — Design

**Date:** 2026-07-24
**Status:** Approved by user (conversation), implementation authorized.

## Goal

Two purely-visual upgrades to dungeon rendering:

1. A **decoration tile layer** painted on its own Tilemap above the ground
   tiles: small details (cracks, pebbles, etc.) scattered over Floor/Corridor
   cells with a configurable density.
2. **Four weighted wall fill variants** instead of the single `wallFill`
   tile, so large wall masses get visual variety.

Both are deterministic per floor seed, cosmetic only (no `FloorData`,
walkability, or gameplay impact), and testable in Edit Mode. The GDD does not
define tile decoration; this is a visual layer with no design implications.

## Decisions (from brainstorming)

- Decorations appear **only over walkable ground** (Floor/Corridor), never on
  `StairsDown` or walls. Random scatter, deterministic per seed.
- Decoration tile assets are **assigned later by the user in the Inspector**;
  the system ships with an empty list and behaves gracefully until then.
- Wall fill variants use **configurable integer weights** (like floor
  variants), defaulting to 25/25/25/25.
- Unassigned wall fill variants (B/C/D null) fall back to the existing
  `wallFill` tile so the scene keeps working before assets are assigned.

## Architecture

Follows the existing pattern: pure selection logic in `TileVisualResolver`
(Edit Mode–testable, no UnityEngine.Tilemaps), tile data in
`DungeonTilesetSO`, painting in `FloorInstantiator`, layering via a separate
Tilemap with its own Sorting Order (same approach as `GridOverlayInstantiator`).

### 1. `DungeonTilesetSO` (Data)

- Keep serialized field `wallFill` (variant A — preserves the existing asset
  assignment). Add `wallFillB`, `wallFillC`, `wallFillD` (`TileBase`) and
  `wallFillWeightA..D` (`[Min(0)] int`, default 25 each).
- New serializable struct `WeightedDecoration { TileBase tile; [Min(0)] int weight; }`,
  a `List<WeightedDecoration> decorations` (default empty) and
  `[Range(0,100)] int decorationDensityPercent` (default 10).
- New API: `GetWallFillVariantTile(int index)` (0–3; null B/C/D → `wallFill`),
  `WallFillWeightA..D` getters, `Decorations` read-only list,
  `DecorationDensityPercent` getter.

### 2. `TileVisualResolver` (Pure logic)

- Extract the existing hash into a private `Hash(int seed, int x, int y, uint salt)`.
  `ResolveFloorVariant` uses salt `0`, preserving its exact current output
  (existing tests must stay green unchanged).
- `int ResolveWallFillVariant(int floorSeed, int x, int y, int weightA, int weightB, int weightC, int weightD)`
  → 0–3. Distinct salt. Total ≤ 0 degenerates to 0. Zero-weight variants are
  unreachable.
- `int ResolveDecorationIndex(int floorSeed, int x, int y, int densityPercent, int[] weights)`
  → −1 for "no decoration", else a weighted index into `weights`. Distinct
  salt. One hash per cell: low bits (`h % 100`) roll density, remaining bits
  (`h / 100`) pick the variant. Null/empty weights, total ≤ 0, or density ≤ 0
  → −1; density ≥ 100 → always decorates (given valid weights).
- The caller builds the `int[] weights` array **once per floor**, not per
  cell (no garbage in the paint loop).

### 3. `FloorInstantiator` (Painting)

- New `[SerializeField] Tilemap decorationTilemap`.
- `PaintTiles` also paints decorations: for each Floor/Corridor cell, roll
  `ResolveDecorationIndex`; on a hit, set the decoration tile on
  `decorationTilemap`. StairsDown and walls never get decorations.
- Wall case in `GetTileBase`: shape `Fill` now routes through
  `ResolveWallFillVariant` + `GetWallFillVariantTile`; the other 12 shapes
  are unchanged.
- `ClearPreviousFloor` clears `decorationTilemap` too.
- Error handling: decorations configured (non-empty list with a positive
  weight) but `decorationTilemap` null → `Debug.LogError` (wiring bug).
  Empty decoration list → silently skip (expected state until assets exist).

### 4. Scene wiring (via MCP, never YAML by hand)

- New GameObject `DecorationTilemap` under the Grid in
  `DungeonPreview.unity`: `Tilemap` + `TilemapRenderer`, Sorting Order
  between the terrain tilemap and the grid overlay.
- Assign it to `FloorInstantiator.decorationTilemap`.
- Tileset asset keeps working with defaults; the user assigns wall fill B/C/D
  and decoration tiles later in the Inspector.

### 5. Tests (Edit Mode)

New test classes mirroring `TileVisualResolverFloorVariantTests` style:

- **Wall fill:** determinism; index always 0–3; frequencies approximate
  weights; zero weight unreachable; all-zero weights → 0; pattern differs
  from floor-variant pattern for the same seed (salt independence).
- **Decorations:** determinism; density 0 → always −1; density 100 with
  valid weights → never −1; null/empty weights → −1; index always in range;
  observed decoration rate approximates density; zero-weight entry
  unreachable.
- Existing floor-variant tests must pass unchanged after the hash refactor
  (regression guard).
