# Decoration Layer + Wall Fill Variants Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Paint seed-deterministic decorations on a separate Tilemap above the ground, and replace the single wall fill tile with four weighted variants.

**Architecture:** Pure selection logic goes in `TileVisualResolver` (Edit Mode–testable, no Tilemaps dependency); tile references/weights in `DungeonTilesetSO`; painting in `FloorInstantiator` against a new `decorationTilemap` layered via Sorting Order (same pattern as `GridOverlayInstantiator`). Spec: `docs/superpowers/specs/2026-07-24-decoration-layer-wallfill-variants-design.md`.

**Tech Stack:** Unity 6000.5.3f1, URP 2D, Unity Test Framework (Edit Mode), MCP for Unity for scene edits.

## Global Constraints

- Never hand-edit `.unity`/prefab YAML — scene changes go through MCP.
- No LINQ / per-cell allocations in paint loops.
- `[SerializeField] private` fields, not public.
- Existing `ResolveFloorVariant` output must not change (existing tests stay green unchanged).
- Decorations are cosmetic only: no `FloorData`, walkability, or gameplay impact.
- Tests run via MCP `run_tests` (EditMode) or Unity Test Runner.

---

### Task 1: `TileVisualResolver.ResolveWallFillVariant`

**Files:**
- Modify: `Assets/Scripts/Runtime/DungeonGen/TileVisualResolver.cs`
- Test: `Assets/Tests/EditMode/TileVisualResolverWallFillVariantTests.cs` (create)

**Interfaces:**
- Consumes: existing hash scheme inside `ResolveFloorVariant`.
- Produces: `public static int ResolveWallFillVariant(int floorSeed, int x, int y, int weightA, int weightB, int weightC, int weightD)` → 0–3. Also private `static uint Hash(int seed, int x, int y, uint salt)` reused by Task 2.

- [ ] **Step 1: Write the failing tests**

Create `Assets/Tests/EditMode/TileVisualResolverWallFillVariantTests.cs`:

```csharp
using NUnit.Framework;
using Jagara.Runtime.DungeonGen;

namespace Jagara.Tests.EditMode
{
    public class TileVisualResolverWallFillVariantTests
    {
        [Test]
        public void SameInputs_AlwaysReturnSameVariant()
        {
            int first = TileVisualResolver.ResolveWallFillVariant(1234, 7, 11, 25, 25, 25, 25);

            for (int i = 0; i < 10; i++)
            {
                Assert.AreEqual(first, TileVisualResolver.ResolveWallFillVariant(1234, 7, 11, 25, 25, 25, 25));
            }
        }

        [Test]
        public void Result_IsAlwaysAValidVariantIndex()
        {
            for (int x = 0; x < 50; x++)
            {
                for (int y = 0; y < 50; y++)
                {
                    int variant = TileVisualResolver.ResolveWallFillVariant(999, x, y, 70, 15, 10, 5);
                    Assert.GreaterOrEqual(variant, 0);
                    Assert.LessOrEqual(variant, 3);
                }
            }
        }

        [Test]
        public void ObservedFrequencies_ApproximateConfiguredWeights()
        {
            const int size = 200;
            var counts = new int[4];
            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    counts[TileVisualResolver.ResolveWallFillVariant(42, x, y, 40, 30, 20, 10)]++;
                }
            }

            const double total = size * size;
            Assert.AreEqual(0.40, counts[0] / total, 0.03, "variant A frequency");
            Assert.AreEqual(0.30, counts[1] / total, 0.03, "variant B frequency");
            Assert.AreEqual(0.20, counts[2] / total, 0.03, "variant C frequency");
            Assert.AreEqual(0.10, counts[3] / total, 0.03, "variant D frequency");
        }

        [Test]
        public void ZeroWeight_MakesVariantUnreachable()
        {
            for (int x = 0; x < 100; x++)
            {
                for (int y = 0; y < 100; y++)
                {
                    Assert.AreNotEqual(3, TileVisualResolver.ResolveWallFillVariant(7, x, y, 40, 40, 20, 0));
                }
            }
        }

        [Test]
        public void AllZeroWeights_AlwaysReturnsVariantA()
        {
            for (int x = 0; x < 20; x++)
            {
                for (int y = 0; y < 20; y++)
                {
                    Assert.AreEqual(0, TileVisualResolver.ResolveWallFillVariant(7, x, y, 0, 0, 0, 0));
                }
            }
        }

        [Test]
        public void WallFillPattern_IsIndependentFromFloorVariantPattern()
        {
            // Same seed and equal-ish weight setups: the two selectors must not
            // be correlated cell-by-cell (different salts). With three shared
            // outcomes, identical salts would make outcomes 0-2 always agree.
            int differing = 0;
            for (int x = 0; x < 50; x++)
            {
                for (int y = 0; y < 50; y++)
                {
                    int floor = TileVisualResolver.ResolveFloorVariant(42, x, y, 1, 1, 1);
                    int wall = TileVisualResolver.ResolveWallFillVariant(42, x, y, 1, 1, 1, 0);
                    if (floor != wall)
                    {
                        differing++;
                    }
                }
            }

            Assert.Greater(differing, 0, "Wall fill selection is correlated with floor variant selection.");
        }
    }
}
```

- [ ] **Step 2: Run Edit Mode tests to verify they fail**

Run via MCP: `run_tests` with mode `EditMode`, filter `TileVisualResolverWallFillVariantTests`.
Expected: compile error — `ResolveWallFillVariant` does not exist. (Unity reports it via console; fix nothing yet, this confirms the test targets the missing API.)

- [ ] **Step 3: Implement `Hash` extraction + `ResolveWallFillVariant`**

In `TileVisualResolver.cs`, extract the hash from `ResolveFloorVariant` and add the new selector:

```csharp
private const uint WallFillSalt = 0x51ED270Bu;

/// <summary>
/// Shared cell hash. Salt 0 reproduces the original ResolveFloorVariant
/// sequence exactly; other consumers must pass a distinct salt so their
/// patterns don't correlate with the floor pattern.
/// </summary>
private static uint Hash(int seed, int x, int y, uint salt)
{
    unchecked
    {
        uint h = (uint)seed ^ salt;
        h ^= (uint)x * 0x9E3779B1u;
        h = (h ^ (h >> 16)) * 0x85EBCA6Bu;
        h ^= (uint)y * 0xC2B2AE35u;
        h = (h ^ (h >> 13)) * 0x27D4EB2Fu;
        h ^= h >> 16;
        return h;
    }
}
```

`ResolveFloorVariant` body becomes (weights logic unchanged):

```csharp
int total = primaryWeight + secondaryWeight + tertiaryWeight;
if (total <= 0)
{
    return 0;
}

unchecked
{
    int roll = (int)(Hash(floorSeed, x, y, 0u) % (uint)total);
    if (roll < primaryWeight)
    {
        return 0;
    }

    return roll < primaryWeight + secondaryWeight ? 1 : 2;
}
```

New method:

```csharp
/// <summary>
/// Deterministically picks a wall fill variant (0 = A ... 3 = D) for the
/// cell at (x, y), weighted by the four integer weights. A zero weight
/// makes that variant unreachable; a non-positive total degenerates to A.
/// Salted so the pattern is independent of the floor variant pattern.
/// </summary>
public static int ResolveWallFillVariant(int floorSeed, int x, int y,
    int weightA, int weightB, int weightC, int weightD)
{
    int total = weightA + weightB + weightC + weightD;
    if (total <= 0)
    {
        return 0;
    }

    unchecked
    {
        int roll = (int)(Hash(floorSeed, x, y, WallFillSalt) % (uint)total);
        if (roll < weightA)
        {
            return 0;
        }

        if (roll < weightA + weightB)
        {
            return 1;
        }

        return roll < weightA + weightB + weightC ? 2 : 3;
    }
}
```

- [ ] **Step 4: Run Edit Mode tests to verify they pass (including regression)**

Run via MCP: `run_tests` mode `EditMode`, no filter (full suite).
Expected: all pass — including the untouched `TileVisualResolverFloorVariantTests`, which proves the hash refactor preserved output.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Runtime/DungeonGen/TileVisualResolver.cs "Assets/Tests/EditMode/TileVisualResolverWallFillVariantTests.cs" "Assets/Tests/EditMode/TileVisualResolverWallFillVariantTests.cs.meta"
git commit -m "Add weighted wall fill variant selection to TileVisualResolver"
```

---

### Task 2: `TileVisualResolver.ResolveDecorationIndex`

**Files:**
- Modify: `Assets/Scripts/Runtime/DungeonGen/TileVisualResolver.cs`
- Test: `Assets/Tests/EditMode/TileVisualResolverDecorationTests.cs` (create)

**Interfaces:**
- Consumes: `Hash(seed, x, y, salt)` from Task 1.
- Produces: `public static int ResolveDecorationIndex(int floorSeed, int x, int y, int densityPercent, int[] weights)` → −1 = no decoration, else index into `weights`.

- [ ] **Step 1: Write the failing tests**

Create `Assets/Tests/EditMode/TileVisualResolverDecorationTests.cs`:

```csharp
using NUnit.Framework;
using Jagara.Runtime.DungeonGen;

namespace Jagara.Tests.EditMode
{
    public class TileVisualResolverDecorationTests
    {
        private static readonly int[] DefaultWeights = { 50, 30, 20 };

        [Test]
        public void SameInputs_AlwaysReturnSameResult()
        {
            int first = TileVisualResolver.ResolveDecorationIndex(1234, 7, 11, 30, DefaultWeights);

            for (int i = 0; i < 10; i++)
            {
                Assert.AreEqual(first, TileVisualResolver.ResolveDecorationIndex(1234, 7, 11, 30, DefaultWeights));
            }
        }

        [Test]
        public void ZeroDensity_NeverDecorates()
        {
            for (int x = 0; x < 50; x++)
            {
                for (int y = 0; y < 50; y++)
                {
                    Assert.AreEqual(-1, TileVisualResolver.ResolveDecorationIndex(7, x, y, 0, DefaultWeights));
                }
            }
        }

        [Test]
        public void FullDensity_AlwaysDecorates()
        {
            for (int x = 0; x < 50; x++)
            {
                for (int y = 0; y < 50; y++)
                {
                    Assert.GreaterOrEqual(TileVisualResolver.ResolveDecorationIndex(7, x, y, 100, DefaultWeights), 0);
                }
            }
        }

        [Test]
        public void NullOrEmptyWeights_NeverDecorates()
        {
            Assert.AreEqual(-1, TileVisualResolver.ResolveDecorationIndex(7, 3, 4, 100, null));
            Assert.AreEqual(-1, TileVisualResolver.ResolveDecorationIndex(7, 3, 4, 100, new int[0]));
        }

        [Test]
        public void AllZeroWeights_NeverDecorates()
        {
            Assert.AreEqual(-1, TileVisualResolver.ResolveDecorationIndex(7, 3, 4, 100, new[] { 0, 0 }));
        }

        [Test]
        public void Result_IsAlwaysMinusOneOrValidIndex()
        {
            for (int x = 0; x < 50; x++)
            {
                for (int y = 0; y < 50; y++)
                {
                    int index = TileVisualResolver.ResolveDecorationIndex(999, x, y, 35, DefaultWeights);
                    Assert.GreaterOrEqual(index, -1);
                    Assert.Less(index, DefaultWeights.Length);
                }
            }
        }

        [Test]
        public void ObservedDecorationRate_ApproximatesDensity()
        {
            const int size = 200;
            int decorated = 0;
            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    if (TileVisualResolver.ResolveDecorationIndex(42, x, y, 30, DefaultWeights) >= 0)
                    {
                        decorated++;
                    }
                }
            }

            Assert.AreEqual(0.30, decorated / (double)(size * size), 0.03, "decoration rate");
        }

        [Test]
        public void ZeroWeightEntry_IsUnreachable()
        {
            var weights = new[] { 60, 0, 40 };
            for (int x = 0; x < 100; x++)
            {
                for (int y = 0; y < 100; y++)
                {
                    Assert.AreNotEqual(1, TileVisualResolver.ResolveDecorationIndex(7, x, y, 100, weights));
                }
            }
        }
    }
}
```

- [ ] **Step 2: Run to verify failure**

MCP `run_tests` EditMode, filter `TileVisualResolverDecorationTests`.
Expected: compile error — `ResolveDecorationIndex` does not exist.

- [ ] **Step 3: Implement `ResolveDecorationIndex`**

Add to `TileVisualResolver.cs`:

```csharp
private const uint DecorationSalt = 0xB5297A4Du;
```

```csharp
/// <summary>
/// Decides whether the ground cell at (x, y) gets a decoration and which
/// one: -1 for none, else a weighted index into <paramref name="weights"/>.
/// One hash per cell: the low bits (mod 100) roll against densityPercent,
/// the remaining bits pick the weighted entry, so density and choice stay
/// independent. Null/empty weights, a non-positive total, or density &lt;= 0
/// never decorate; density &gt;= 100 always does. The caller is expected to
/// build the weights array once per floor, not per cell.
/// </summary>
public static int ResolveDecorationIndex(int floorSeed, int x, int y,
    int densityPercent, int[] weights)
{
    if (weights == null || weights.Length == 0 || densityPercent <= 0)
    {
        return -1;
    }

    int total = 0;
    for (int i = 0; i < weights.Length; i++)
    {
        total += weights[i];
    }

    if (total <= 0)
    {
        return -1;
    }

    unchecked
    {
        uint h = Hash(floorSeed, x, y, DecorationSalt);
        if ((int)(h % 100u) >= densityPercent)
        {
            return -1;
        }

        int roll = (int)((h / 100u) % (uint)total);
        for (int i = 0; i < weights.Length; i++)
        {
            roll -= weights[i];
            if (roll < 0)
            {
                return i;
            }
        }

        return weights.Length - 1; // unreachable with consistent totals
    }
}
```

- [ ] **Step 4: Run full Edit Mode suite, verify all pass**

MCP `run_tests` EditMode, no filter. Expected: all pass.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Runtime/DungeonGen/TileVisualResolver.cs "Assets/Tests/EditMode/TileVisualResolverDecorationTests.cs" "Assets/Tests/EditMode/TileVisualResolverDecorationTests.cs.meta"
git commit -m "Add deterministic decoration selection to TileVisualResolver"
```

---

### Task 3: `DungeonTilesetSO` wall fill variants + decoration list

**Files:**
- Modify: `Assets/Scripts/Runtime/Data/DungeonTilesetSO.cs`
- Test: `Assets/Tests/EditMode/DungeonTilesetSOTests.cs` (create)

**Interfaces:**
- Produces (used by Task 4):
  - `public TileBase GetWallFillVariantTile(int variantIndex)` — 0–3; null B/C/D fall back to `wallFill`.
  - `public int WallFillWeightA/B/C/D { get; }`
  - `public struct WeightedDecoration { public TileBase Tile; public int Weight; }` (serialized fields `tile`/`weight` exposed via properties is overkill for a data struct — use public fields `tile`, `weight` to match Unity serialization simplicity).
  - `public IReadOnlyList<WeightedDecoration> Decorations { get; }`
  - `public int DecorationDensityPercent { get; }`

- [ ] **Step 1: Write the failing tests**

Create `Assets/Tests/EditMode/DungeonTilesetSOTests.cs`. `DungeonTilesetSO`'s fields are private; the test uses a fresh instance (all tiles null) plus `SerializedObject`-free reflection-light checks — keep it behavioral: fallback and defaults only.

```csharp
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;
using Jagara.Runtime.Data;

namespace Jagara.Tests.EditMode
{
    public class DungeonTilesetSOTests
    {
        private DungeonTilesetSO tileset;

        [SetUp]
        public void SetUp()
        {
            tileset = ScriptableObject.CreateInstance<DungeonTilesetSO>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(tileset);
        }

        [Test]
        public void WallFillWeights_DefaultToEqualDistribution()
        {
            Assert.AreEqual(25, tileset.WallFillWeightA);
            Assert.AreEqual(25, tileset.WallFillWeightB);
            Assert.AreEqual(25, tileset.WallFillWeightC);
            Assert.AreEqual(25, tileset.WallFillWeightD);
        }

        [Test]
        public void Decorations_DefaultToEmptyWithModestDensity()
        {
            Assert.AreEqual(0, tileset.Decorations.Count);
            Assert.AreEqual(10, tileset.DecorationDensityPercent);
        }

        [Test]
        public void GetWallFillVariantTile_UnassignedVariants_FallBackToVariantA()
        {
            var fillA = ScriptableObject.CreateInstance<Tile>();
            try
            {
                SetPrivateField(tileset, "wallFill", fillA);

                for (int i = 0; i < 4; i++)
                {
                    Assert.AreSame(fillA, tileset.GetWallFillVariantTile(i), $"variant {i}");
                }
            }
            finally
            {
                Object.DestroyImmediate(fillA);
            }
        }

        [Test]
        public void GetWallFillVariantTile_AssignedVariant_IsReturned()
        {
            var fillA = ScriptableObject.CreateInstance<Tile>();
            var fillB = ScriptableObject.CreateInstance<Tile>();
            try
            {
                SetPrivateField(tileset, "wallFill", fillA);
                SetPrivateField(tileset, "wallFillB", fillB);

                Assert.AreSame(fillB, tileset.GetWallFillVariantTile(1));
                Assert.AreSame(fillA, tileset.GetWallFillVariantTile(2), "unassigned C falls back");
            }
            finally
            {
                Object.DestroyImmediate(fillA);
                Object.DestroyImmediate(fillB);
            }
        }

        private static void SetPrivateField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(field, $"field '{name}' not found");
            field.SetValue(target, value);
        }
    }
}
```

- [ ] **Step 2: Run to verify failure**

MCP `run_tests` EditMode, filter `DungeonTilesetSOTests`.
Expected: compile error — `WallFillWeightA` etc. do not exist.

- [ ] **Step 3: Implement the SO changes**

In `DungeonTilesetSO.cs`, add `using System.Collections.Generic;`, replace the `[Header("Wall - Fill")]` block with:

```csharp
[Header("Wall - Fill variants (weighted; unassigned fall back to A)")]
[SerializeField] private TileBase wallFill;
[SerializeField] private TileBase wallFillB;
[SerializeField] private TileBase wallFillC;
[SerializeField] private TileBase wallFillD;
[SerializeField, Min(0)] private int wallFillWeightA = 25;
[SerializeField, Min(0)] private int wallFillWeightB = 25;
[SerializeField, Min(0)] private int wallFillWeightC = 25;
[SerializeField, Min(0)] private int wallFillWeightD = 25;
```

After the Floor Variants block add:

```csharp
[Header("Decorations (painted on a separate tilemap over walkable ground)")]
[SerializeField] private List<WeightedDecoration> decorations = new();
[SerializeField, Range(0, 100)] private int decorationDensityPercent = 10;
```

Nested type + API (place the struct above the fields, getters next to the existing ones):

```csharp
[System.Serializable]
public struct WeightedDecoration
{
    public TileBase tile;
    [Min(0)] public int weight;
}
```

```csharp
public int WallFillWeightA => wallFillWeightA;
public int WallFillWeightB => wallFillWeightB;
public int WallFillWeightC => wallFillWeightC;
public int WallFillWeightD => wallFillWeightD;
public IReadOnlyList<WeightedDecoration> Decorations => decorations;
public int DecorationDensityPercent => decorationDensityPercent;

/// <summary>
/// Wall fill variant lookup (0 = A ... 3 = D). Variants without an
/// assigned tile fall back to variant A so the dungeon renders before
/// all four assets exist.
/// </summary>
public TileBase GetWallFillVariantTile(int variantIndex)
{
    TileBase tile;
    switch (variantIndex)
    {
        case 1:
            tile = wallFillB;
            break;
        case 2:
            tile = wallFillC;
            break;
        case 3:
            tile = wallFillD;
            break;
        default:
            tile = wallFill;
            break;
    }

    return tile != null ? tile : wallFill;
}
```

- [ ] **Step 4: Run full Edit Mode suite, verify all pass**

MCP `run_tests` EditMode, no filter. Expected: all pass.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Runtime/Data/DungeonTilesetSO.cs "Assets/Tests/EditMode/DungeonTilesetSOTests.cs" "Assets/Tests/EditMode/DungeonTilesetSOTests.cs.meta"
git commit -m "Add wall fill variants and decoration list to DungeonTilesetSO"
```

---

### Task 4: `FloorInstantiator` paints decorations + wall fill variants

**Files:**
- Modify: `Assets/Scripts/Runtime/DungeonGen/FloorInstantiator.cs`

**Interfaces:**
- Consumes: `ResolveWallFillVariant` (Task 1), `ResolveDecorationIndex` (Task 2), `GetWallFillVariantTile` / `Decorations` / `DecorationDensityPercent` / `WallFillWeightA..D` (Task 3).
- Produces: new serialized field `decorationTilemap` (wired in Task 5).

No new Edit Mode tests — MonoBehaviour painting is verified via MCP in Task 5 (per project testing policy).

- [ ] **Step 1: Add the decoration tilemap field**

```csharp
[SerializeField] private Tilemap decorationTilemap;
```

(next to the existing `tilemap` field.)

- [ ] **Step 2: Route `WallShape.Fill` through the variant selector**

Replace the `case TileType.Wall:` branch of `GetTileBase`:

```csharp
case TileType.Wall:
    WallShape shape = TileVisualResolver.ResolveWallShape(floor.Grid, x, y);
    if (shape == WallShape.Fill)
    {
        return tileset.GetWallFillVariantTile(TileVisualResolver.ResolveWallFillVariant(
            floor.Seed, x, y,
            tileset.WallFillWeightA, tileset.WallFillWeightB,
            tileset.WallFillWeightC, tileset.WallFillWeightD));
    }

    return tileset.GetWallTile(shape);
```

- [ ] **Step 3: Paint decorations in `PaintTiles` and clear them in `ClearPreviousFloor`**

`PaintTiles` becomes:

```csharp
private void PaintTiles(FloorData floor)
{
    int width = floor.Grid.GetLength(0);
    int height = floor.Grid.GetLength(1);
    int[] decorationWeights = BuildDecorationWeights();

    for (int x = 0; x < width; x++)
    {
        for (int y = 0; y < height; y++)
        {
            // FloorData's grid has no inherent up/down orientation, so grid (x, y)
            // maps straight to Tilemap cell (x, y) with no flip. This means the
            // in-scene layout is vertically mirrored relative to FloorData.ToAsciiArt()'s
            // printed text (row 0 is top-of-text but bottom-of-world, since +Y is up) -
            // that mismatch is cosmetic only and not a bug.
            tilemap.SetTile(new Vector3Int(x, y, 0), GetTileBase(floor, x, y));
            PaintDecoration(floor, x, y, decorationWeights);
        }
    }
}

/// <summary>
/// Weights array built once per floor so the per-cell decoration roll
/// allocates nothing. Null when decorations are unconfigured or unusable
/// (also null — with an error — when configured but the tilemap is missing).
/// </summary>
private int[] BuildDecorationWeights()
{
    var decorations = tileset.Decorations;
    if (decorations.Count == 0)
    {
        return null;
    }

    if (decorationTilemap == null)
    {
        Debug.LogError("FloorInstantiator: tileset has decorations but decorationTilemap is not assigned.");
        return null;
    }

    var weights = new int[decorations.Count];
    for (int i = 0; i < decorations.Count; i++)
    {
        weights[i] = decorations[i].weight;
    }

    return weights;
}

private void PaintDecoration(FloorData floor, int x, int y, int[] weights)
{
    if (weights == null)
    {
        return;
    }

    TileType tileType = floor.Grid[x, y];
    if (tileType != TileType.Floor && tileType != TileType.Corridor)
    {
        return;
    }

    int index = TileVisualResolver.ResolveDecorationIndex(
        floor.Seed, x, y, tileset.DecorationDensityPercent, weights);
    if (index < 0)
    {
        return;
    }

    TileBase tile = tileset.Decorations[index].tile;
    if (tile != null)
    {
        decorationTilemap.SetTile(new Vector3Int(x, y, 0), tile);
    }
}
```

In `ClearPreviousFloor`, after `tilemap.ClearAllTiles();`:

```csharp
if (decorationTilemap != null)
{
    decorationTilemap.ClearAllTiles();
}
```

- [ ] **Step 4: Compile check + full Edit Mode suite**

MCP `refresh_unity` + `read_console` for compile errors, then `run_tests` EditMode. Expected: clean compile, all tests pass.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Runtime/DungeonGen/FloorInstantiator.cs
git commit -m "Paint decoration layer and weighted wall fill in FloorInstantiator"
```

---

### Task 5: Scene wiring via MCP + verification

**Files:**
- Modify (via MCP only): `Assets/Scenes/DungeonPreview.unity`

**Interfaces:**
- Consumes: `FloorInstantiator.decorationTilemap` serialized field (Task 4).

- [ ] **Step 1: Inspect current scene layering via MCP**

Use `manage_scene`/`find_gameobjects` to find the Grid, terrain Tilemap and grid-overlay Tilemap, and read the overlay `TilemapRenderer` sorting orders.

- [ ] **Step 2: Create `DecorationTilemap` under the Grid**

Via MCP `manage_gameobject`/`manage_components`: new child of the Grid named `DecorationTilemap` with `Tilemap` + `TilemapRenderer`. Set its Sorting Order strictly between the terrain tilemap's and the grid overlay's (e.g. terrain 0, overlay 10 → decoration 5 — use actual observed values).

- [ ] **Step 3: Assign the reference**

Via MCP, set `FloorInstantiator.decorationTilemap` to the new Tilemap in `DungeonPreview.unity`, then save the scene.

- [ ] **Step 4: Verify**

- `read_console`: no errors/warnings from the change.
- Full Edit Mode suite via `run_tests`: all pass.
- Regenerate the preview floor (Editor stopped, per project policy) and confirm: no console errors; walls render (fill variants fall back to A until B/C/D are assigned); no decorations painted yet (list is empty — expected).

- [ ] **Step 5: Commit**

```bash
git add Assets/Scenes/DungeonPreview.unity
git commit -m "Add DecorationTilemap layer to DungeonPreview scene"
```
