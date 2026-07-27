# Torch Flicker Light — Design

Date: 2026-07-24
Status: Approved

## Context

The player already has a low-intensity Global Light 2D in `DungeonPreview.unity` for testing 2D lighting (the Tilemap's `TilemapRenderer` material was switched from `Sprite-Unlit-Default` to `Sprite-Lit-Default` in a prior session so it responds to `Light2D`). The player now needs its own light source — an omnidirectional torch-style glow that flickers, so the player can see their immediate surroundings independent of the global light level.

This is purely a visual/ambiance feature. There is no fog-of-war or vision-radius mechanic in the project yet, and this design does not introduce one — the torch light is cosmetic only, not gameplay-coupled.

## Goals

- Player carries a warm, omnidirectional light (not a directional cone) that illuminates the area around them.
- The light flickers using Perlin noise for an organic, torch-like effect (intensity, plus a smaller radius jitter).
- The flicker logic is reusable for future light sources (wall torches, campfires, enemy lanterns) without modifying player-specific code.
- The core flicker math is unit-testable in Edit Mode, consistent with the project's generation/instantiation split pattern.

## Non-goals

- No fog-of-war / vision-radius gameplay mechanic — the light does not gate what the player can see or interact with, it's purely visual.
- No directional/cone spotlight behavior.
- No ScriptableObject-based flicker presets (Approach 3 considered and deferred — plain serialized fields on the component are sufficient for a single light profile today; presets can be added later without breaking this design if multiple flicker "moods" are needed).

## Architecture

### Prefab structure

`Assets/Prefabs/Player.prefab` gains one new child GameObject:

```
Player (existing root: Transform, SpriteRenderer, PlayerGridMover, PlayerController)
└── TorchLight (new)
    ├── Light2D (URP 2D Renderer component)
    └── FlickeringLight2D (new script)
```

- `TorchLight` local position: `(0, 0, 0)` — centered on the player.
- `Light2D` settings:
  - Light Type: Point (labeled "Spot" in the URP 2D inspector) with **Outer Angle = 360°**, giving an omnidirectional radial glow rather than a directional cone.
  - Color: warm amber/orange (placeholder: `#FFB347`, tune visually).
  - Outer Radius: ~3.5 units (grid cell size is 1 unit, per `Tilemap.cellSize`), Inner Radius smaller for a soft core.
  - Intensity: driven at runtime by `FlickeringLight2D`, starting baseline ~1.2.

### Scripts

**`Assets/Scripts/Runtime/Gameplay/TorchFlicker.cs`** — static pure logic, no UnityEngine.Rendering dependency beyond `Mathf`/`UnityEngine` types already used elsewhere in `Gameplay/`:

```csharp
public static class TorchFlicker
{
    public static float ComputeValue(float time, float seed, float speed, float baseValue, float amplitude)
        => baseValue + (Mathf.PerlinNoise(time * speed + seed, 0f) * 2f - 1f) * amplitude;
}
```

- Deterministic given `(time, seed, speed, baseValue, amplitude)`.
- Output is bounded to `[baseValue - amplitude, baseValue + amplitude]` (Perlin noise output range `[0,1]` mapped to `[-1,1]` then scaled).

**`Assets/Scripts/Runtime/Gameplay/FlickeringLight2D.cs`** — MonoBehaviour wrapper:

- `[SerializeField] private Light2D targetLight;` — auto-populated via `GetComponent<Light2D>()` in `Awake` if left unassigned; `Debug.LogError` and disable the component if still null (consistent with existing guard-clause style, e.g. `FloorInstantiator`).
- `[SerializeField] private float baseIntensity = 1.2f;`
- `[SerializeField] private float intensityAmplitude = 0.3f;`
- `[SerializeField] private float baseOuterRadius = 3.5f;`
- `[SerializeField] private float radiusAmplitude = 0.35f;` (~10% of base radius)
- `[SerializeField] private float noiseSpeed = 1.5f;`
- Private `intensitySeed`, `radiusSeed` (each `Random.value * 1000f`, assigned in `Awake`) — so multiple torch instances in a scene don't flicker in sync.
- `Update()`:
  ```csharp
  targetLight.intensity = TorchFlicker.ComputeValue(Time.time, intensitySeed, noiseSpeed, baseIntensity, intensityAmplitude);
  targetLight.pointLightOuterRadius = TorchFlicker.ComputeValue(Time.time, radiusSeed, noiseSpeed * 0.7f, baseOuterRadius, radiusAmplitude);
  ```
  (Radius uses a slightly different speed multiplier than intensity so the two don't move in lockstep.)
- No `FixedUpdate`, no per-frame `GetComponent` calls (cached reference only).

### Data flow / coupling

- `FlickeringLight2D` has no reference to `PlayerController`, `TurnResolver`, or any turn-system type. It flickers every rendered frame regardless of whose turn it is — this is deliberate, since it's ambiance, not gameplay state.
- No changes needed to `NightmareBootstrap.cs` — the light travels with the player automatically since `TorchLight` is a child of the `Player.prefab` root that `NightmareBootstrap.SpawnPlayer` already instantiates.

## Testing

- **Edit Mode test** (`Assets/Tests/EditMode/TorchFlickerTests.cs`): pure-logic tests against `TorchFlicker.ComputeValue` —
  - Output stays within `[baseValue - amplitude, baseValue + amplitude]` across a range of sampled `time` values.
  - Same inputs → same output (determinism).
  - Different `seed` values with the same `time` produce different outputs (decorrelation check, so two torches don't sync).
- **Manual/Play Mode verification** (via MCP, per CLAUDE.md): enter Play Mode, confirm the `TorchLight` visually flickers around the player and that `Light2D.intensity`/`pointLightOuterRadius` change over time. Structural prefab changes (adding `TorchLight`, `Light2D`, `FlickeringLight2D`) are made via MCP tools with the Editor stopped, not by hand-editing prefab YAML, and not verified while in Play Mode (per CLAUDE.md: Play Mode changes don't persist).

## Open questions / follow-ups (not blocking this design)

- Exact color/radius/intensity tuning is a visual judgment call to be made in the Editor once the light exists — the numeric defaults above are starting points, not final.
- If the game later needs multiple flicker "moods" (weak candle vs. roaring torch) or non-player flickering lights (wall sconces), promote the per-instance fields into a shared `TorchLightPresetSO` (Approach 3 from the brainstorm) — deferred for now per YAGNI.
