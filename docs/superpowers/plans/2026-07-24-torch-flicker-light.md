# Torch Flicker Light Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give the player a warm, omnidirectional Light2D that flickers like a torch, using Perlin noise, without coupling to the turn system or gameplay logic.

**Architecture:** A pure static class (`TorchFlicker`) computes the noise-driven value; a thin `MonoBehaviour` (`FlickeringLight2D`) applies it to a `Light2D` component's `intensity` and `pointLightOuterRadius` every frame. The `Light2D` + `FlickeringLight2D` pair lives on a new `TorchLight` child GameObject under `Assets/Prefabs/Player.prefab`, so it travels with the player automatically (no changes needed to `NightmareBootstrap.cs`).

**Tech Stack:** Unity 6000.5.3f1, URP 2D Renderer (`Light2D` from package `com.unity.render-pipelines.universal`, assembly `Unity.RenderPipelines.Universal.2D.Runtime`), NUnit via Unity Test Framework, MCP for Unity (structural prefab edits).

## Global Constraints

- Never hand-edit `.unity`/`.prefab` YAML directly — use MCP for Unity tools for all structural GameObject/component/prefab changes (per `CLAUDE.md`).
- `[SerializeField] private` for Inspector-exposed fields; no `GetComponent` calls inside `Update()` (cache in `Awake()`).
- This feature is cosmetic only — no fog-of-war/vision-radius coupling. Flag this explicitly when reporting completion (per `CLAUDE.md`'s guidance on `[PROVISIONAL]`/`[PENDIENTE]` scope).
- Spec: `docs/superpowers/specs/2026-07-24-torch-flicker-light-design.md`.

---

### Task 1: `TorchFlicker` pure noise logic + Edit Mode tests

**Files:**
- Create: `Assets/Scripts/Runtime/Gameplay/TorchFlicker.cs`
- Create: `Assets/Tests/EditMode/TorchFlickerTests.cs`

**Interfaces:**
- Produces: `Jagara.Runtime.Gameplay.TorchFlicker.ComputeValue(float time, float seed, float speed, float baseValue, float amplitude) -> float`. Later tasks (Task 2's `FlickeringLight2D`) call this exact signature.

- [ ] **Step 1: Write the failing test file**

Create `Assets/Tests/EditMode/TorchFlickerTests.cs`:

```csharp
using NUnit.Framework;
using Jagara.Runtime.Gameplay;

namespace Jagara.Tests.EditMode
{
    public class TorchFlickerTests
    {
        [TestCase(0f)]
        [TestCase(1.37f)]
        [TestCase(52.9f)]
        [TestCase(1000f)]
        public void ComputeValue_StaysWithinAmplitudeBounds(float time)
        {
            const float baseValue = 1.2f;
            const float amplitude = 0.3f;

            float value = TorchFlicker.ComputeValue(time, seed: 42f, speed: 1.5f, baseValue, amplitude);

            Assert.GreaterOrEqual(value, baseValue - amplitude);
            Assert.LessOrEqual(value, baseValue + amplitude);
        }

        [Test]
        public void ComputeValue_IsDeterministic_ForSameInputs()
        {
            float a = TorchFlicker.ComputeValue(12.34f, 7f, 1.5f, 1.2f, 0.3f);
            float b = TorchFlicker.ComputeValue(12.34f, 7f, 1.5f, 1.2f, 0.3f);

            Assert.AreEqual(a, b);
        }

        [Test]
        public void ComputeValue_DifferentSeeds_ProduceDifferentValues()
        {
            // Non-integer time/seed values deliberately avoid Perlin lattice points
            // (integer x with y=0 degenerates to the same value regardless of seed).
            float valueA = TorchFlicker.ComputeValue(10.37f, 1.23f, 1.5f, 1.2f, 0.3f);
            float valueB = TorchFlicker.ComputeValue(10.37f, 500.87f, 1.5f, 1.2f, 0.3f);

            Assert.AreNotEqual(valueA, valueB);
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Use the `mcp__UnityMCP__run_tests` tool:
- `mode`: `"EditMode"`
- `assembly_names`: `["Jagara.Tests.EditMode"]`
- `test_names`: `["Jagara.Tests.EditMode.TorchFlickerTests"]`
- `include_failed_tests`: `true`

This returns a `job_id` — poll it with `mcp__UnityMCP__get_test_job` (`wait_timeout: 30`).

Expected: compile error — `TorchFlicker` does not exist in namespace `Jagara.Runtime.Gameplay`. (If the runner reports 0 tests found instead of a compile error, check `read_console` for the CS0246 error and confirm that's what's blocking the run — either result confirms the test can't pass yet.)

- [ ] **Step 3: Implement `TorchFlicker`**

Create `Assets/Scripts/Runtime/Gameplay/TorchFlicker.cs`:

```csharp
using UnityEngine;

namespace Jagara.Runtime.Gameplay
{
    /// <summary>
    /// Pure Perlin-noise flicker math, decoupled from MonoBehaviour/Light2D so it
    /// can run in Edit Mode tests. See FlickeringLight2D for the runtime wrapper.
    /// </summary>
    public static class TorchFlicker
    {
        public static float ComputeValue(float time, float seed, float speed, float baseValue, float amplitude)
        {
            float noise = Mathf.PerlinNoise(time * speed + seed, 0f);
            return baseValue + (noise * 2f - 1f) * amplitude;
        }
    }
}
```

- [ ] **Step 4: Run the tests again to verify they pass**

Repeat Step 2's `mcp__UnityMCP__run_tests` call (same parameters).

Expected: all 6 test cases (4 `TestCase` variants of `ComputeValue_StaysWithinAmplitudeBounds` + 2 single tests) pass, 0 failures.

- [ ] **Step 5: Commit**

```bash
git add "Assets/Scripts/Runtime/Gameplay/TorchFlicker.cs" "Assets/Tests/EditMode/TorchFlickerTests.cs" "Assets/Tests/EditMode/TorchFlickerTests.cs.meta" "Assets/Scripts/Runtime/Gameplay/TorchFlicker.cs.meta"
git commit -m "Add TorchFlicker pure noise logic with Edit Mode tests"
```

(Unity generates the `.meta` files automatically on the asset-database refresh that follows test discovery/compilation in Step 2 — confirm they exist with `ls Assets/Scripts/Runtime/Gameplay/TorchFlicker.cs.meta Assets/Tests/EditMode/TorchFlickerTests.cs.meta` before staging; if either is missing, trigger `mcp__UnityMCP__refresh_unity` (`scope: "assets"`) first.)

---

### Task 2: `FlickeringLight2D` MonoBehaviour wrapper

**Files:**
- Modify: `Assets/Scripts/Runtime/Jagara.Runtime.asmdef`
- Create: `Assets/Scripts/Runtime/Gameplay/FlickeringLight2D.cs`

**Interfaces:**
- Consumes: `TorchFlicker.ComputeValue(float, float, float, float, float) -> float` (Task 1).
- Produces: `Jagara.Runtime.Gameplay.FlickeringLight2D`, a `[RequireComponent(typeof(Light2D))]` MonoBehaviour with serialized fields `targetLight`, `baseIntensity`, `intensityAmplitude`, `baseOuterRadius`, `radiusAmplitude`, `noiseSpeed`. Task 3 adds this component to the `TorchLight` GameObject and relies on these exact field names when setting Inspector values via MCP.

This task has no automated test: it drives a `Light2D` (a Unity Editor/render-pipeline type) from `Time.time` inside `Update()`, which isn't meaningfully testable outside Play Mode without disproportionate scaffolding for a cosmetic effect. Verification here is "it compiles cleanly," per the spec's testing section — visual/behavioral verification happens in Task 4.

- [ ] **Step 1: Add the URP 2D Renderer assembly reference**

Read `Assets/Scripts/Runtime/Jagara.Runtime.asmdef`, then edit its `"references"` array from:

```json
    "references": [
        "Unity.InputSystem"
    ],
```

to:

```json
    "references": [
        "Unity.InputSystem",
        "Unity.RenderPipelines.Universal.2D.Runtime"
    ],
```

- [ ] **Step 2: Create `FlickeringLight2D.cs`**

Create `Assets/Scripts/Runtime/Gameplay/FlickeringLight2D.cs`:

```csharp
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Jagara.Runtime.Gameplay
{
    /// <summary>
    /// Drives a Light2D's intensity and outer radius with independent Perlin-noise
    /// flicker (see TorchFlicker), so multiple instances don't pulse in sync.
    /// </summary>
    [RequireComponent(typeof(Light2D))]
    public class FlickeringLight2D : MonoBehaviour
    {
        [SerializeField] private Light2D targetLight;
        [SerializeField] private float baseIntensity = 1.2f;
        [SerializeField] private float intensityAmplitude = 0.3f;
        [SerializeField] private float baseOuterRadius = 3.5f;
        [SerializeField] private float radiusAmplitude = 0.35f;
        [SerializeField] private float noiseSpeed = 1.5f;

        private float intensitySeed;
        private float radiusSeed;

        private void Awake()
        {
            if (targetLight == null)
            {
                targetLight = GetComponent<Light2D>();
            }

            if (targetLight == null)
            {
                Debug.LogError("FlickeringLight2D: no Light2D found on this GameObject.");
                enabled = false;
                return;
            }

            intensitySeed = Random.value * 1000f;
            radiusSeed = Random.value * 1000f;
        }

        private void Update()
        {
            targetLight.intensity = TorchFlicker.ComputeValue(
                Time.time, intensitySeed, noiseSpeed, baseIntensity, intensityAmplitude);

            targetLight.pointLightOuterRadius = TorchFlicker.ComputeValue(
                Time.time, radiusSeed, noiseSpeed * 0.7f, baseOuterRadius, radiusAmplitude);
        }
    }
}
```

- [ ] **Step 3: Trigger a Unity recompile and check for errors**

Call `mcp__UnityMCP__refresh_unity` with `scope: "scripts"`, `compile: "request"`, `wait_for_ready: true`.

Then call `mcp__UnityMCP__read_console` with `types: ["error"]`, `count: "20"`.

Expected: no errors referencing `FlickeringLight2D.cs` or `Jagara.Runtime.asmdef`. If you see `CS0246: The type or namespace name 'Light2D' could not be found`, re-check Step 1's asmdef edit was saved and re-run this step.

- [ ] **Step 4: Commit**

```bash
git add "Assets/Scripts/Runtime/Jagara.Runtime.asmdef" "Assets/Scripts/Runtime/Gameplay/FlickeringLight2D.cs" "Assets/Scripts/Runtime/Gameplay/FlickeringLight2D.cs.meta"
git commit -m "Add FlickeringLight2D MonoBehaviour wrapper around TorchFlicker"
```

---

### Task 3: Wire `TorchLight` into `Player.prefab`

**Files:**
- Modify: `Assets/Prefabs/Player.prefab` (via MCP only — no direct YAML edits)

**Interfaces:**
- Consumes: `Jagara.Runtime.Gameplay.FlickeringLight2D` (Task 2) and `UnityEngine.Rendering.Universal.Light2D` (URP package).
- Produces: a `TorchLight` child GameObject under the `Player` prefab root, carrying both components, configured and ready for Task 4's Play Mode check.

- [ ] **Step 1: Open the prefab in isolation (Prefab Stage)**

Call `mcp__UnityMCP__manage_prefabs`:
- `action`: `"open_prefab_stage"`
- `prefab_path`: `"Assets/Prefabs/Player.prefab"`

- [ ] **Step 2: Create the `TorchLight` child GameObject**

Call `mcp__UnityMCP__manage_gameobject`:
- `action`: `"create"`
- `name`: `"TorchLight"`
- `parent`: `"Player"`
- `position`: `[0, 0, 0]`

Note the returned instance ID (used as `target` in the following steps) — call it `TORCH_LIGHT_ID` below.

- [ ] **Step 3: Add and configure the `Light2D` component**

Call `mcp__UnityMCP__manage_components`:
- `action`: `"add"`
- `target`: `TORCH_LIGHT_ID`
- `search_method`: `"by_id"`
- `component_type`: `"UnityEngine.Rendering.Universal.Light2D"`

Then call `mcp__UnityMCP__manage_components` again:
- `action`: `"set_property"`
- `target`: `TORCH_LIGHT_ID`
- `search_method`: `"by_id"`
- `component_type`: `"UnityEngine.Rendering.Universal.Light2D"`
- `properties`:
  ```json
  {
    "lightType": 3,
    "color": {"r": 1.0, "g": 0.7, "b": 0.35, "a": 1.0},
    "intensity": 1.2,
    "pointLightInnerAngle": 360,
    "pointLightOuterAngle": 360,
    "pointLightInnerRadius": 0.5,
    "pointLightOuterRadius": 3.5
  }
  ```

(`lightType: 3` is `Light2D.LightType.Point`, which the URP 2D inspector displays as "Spot" — with a 360° outer angle it renders as an omnidirectional glow, matching the approved design. If `set_property` rejects the enum as an int, retry with `"lightType": "Point"`.)

- [ ] **Step 4: Add the `FlickeringLight2D` component**

Call `mcp__UnityMCP__manage_components`:
- `action`: `"add"`
- `target`: `TORCH_LIGHT_ID`
- `search_method`: `"by_id"`
- `component_type`: `"Jagara.Runtime.Gameplay.FlickeringLight2D"`

Leave its fields at their script defaults (`baseIntensity: 1.2`, `intensityAmplitude: 0.3`, `baseOuterRadius: 3.5`, `radiusAmplitude: 0.35`, `noiseSpeed: 1.5`) — these already match the `Light2D` values set in Step 3, and `targetLight` auto-resolves via `GetComponent<Light2D>()` in `Awake()`, so no object-reference wiring is needed here.

- [ ] **Step 5: Verify the hierarchy**

Call `mcp__UnityMCP__manage_prefabs`:
- `action`: `"get_hierarchy"`
- `prefab_path`: `"Assets/Prefabs/Player.prefab"`

Expected: `Player` root has one child, `TorchLight`, carrying `Light2D` and `FlickeringLight2D`.

- [ ] **Step 6: Save and close the prefab stage**

Call `mcp__UnityMCP__manage_prefabs` with `action: "save_prefab_stage"`, then again with `action: "close_prefab_stage"`.

- [ ] **Step 7: Commit**

```bash
git add "Assets/Prefabs/Player.prefab"
git commit -m "Attach flickering torch light to Player prefab"
```

---

### Task 4: Play Mode verification

**Files:** none (verification only — no code changes).

- [ ] **Step 1: Confirm the scene is stopped, then enter Play Mode**

Call `mcp__UnityMCP__manage_scene` with `action: "get_active"` to confirm `DungeonPreview.unity` is the active scene, then call `mcp__UnityMCP__manage_editor` with `action: "play"`.

- [ ] **Step 2: Check the console for runtime errors**

Call `mcp__UnityMCP__read_console` with `types: ["error"]`, `count: "20"`.

Expected: no errors from `FlickeringLight2D` (e.g. "no Light2D found") or `NightmareBootstrap`.

- [ ] **Step 3: Capture two screenshots a few seconds apart to confirm flicker**

Call `mcp__UnityMCP__manage_camera`:
- `action`: `"screenshot"`
- `include_image`: `true`

Wait roughly 2 seconds (the noise moves visibly within that window at `noiseSpeed: 1.5`), then call it again with the same parameters.

Expected: the player is surrounded by a warm, roughly circular glow on the tilemap (not just the player sprite lit — this is the earlier Tilemap-material fix paying off), and the glow's brightness/radius visibly differs between the two screenshots.

- [ ] **Step 4: Exit Play Mode**

Call `mcp__UnityMCP__manage_editor` with `action: "stop"`.

(Per `CLAUDE.md`, Play Mode changes don't persist — nothing here needs saving. If Step 2 or Step 3 revealed a problem, fix it with the Editor stopped, per Task 3's steps, then re-run this task from Step 1.)

- [ ] **Step 5: Report completion**

No commit in this task (verification only). Summarize for the user: confirm the torch flickers as designed, and explicitly flag that this remains a cosmetic-only feature (no vision/fog-of-war coupling), per the spec's non-goals.
