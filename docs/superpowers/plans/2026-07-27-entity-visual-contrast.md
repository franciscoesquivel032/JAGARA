# Entity & Environment Visual Contrast Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the player and enemies visually distinct from each other and from the dungeon environment/decoration.

**Architecture:** Two independent, additive visual layers: (1) a shared URP sprite shader that draws a per-instance colored outline plus a small saturation/brightness boost on the Player and Enemy prefabs (color set via `MaterialPropertyBlock`, no material instancing), and (2) a theme-driven multiply tint applied to the floor and decoration `Tilemap`s that dims the environment without touching entity sprites. Neither layer depends on the other; a `Faction`-style concept is deliberately **not** introduced — colors are hardcoded per-prefab (Player = gold, Enemy = red) per the approved spec.

**Tech Stack:** Unity 6000.5.3f1, URP (2D Renderer), Input System (unrelated to this work), NUnit via Unity Test Framework (Edit Mode + Play Mode), MCP for Unity (`mcp__UnityMCP__*` tools) for all shader/material/prefab asset operations.

## Global Constraints

- Never hand-edit `.prefab`/`.asset`/`.unity` YAML directly — all prefab, material, and ScriptableObject asset changes go through MCP tools (`manage_prefabs`, `manage_material`, `manage_scriptable_object`, `manage_shader`). Source spec: `CLAUDE.md` "Working with MCP for Unity".
- `.cs` scripts are edited directly with the Read/Write/Edit tools (this is not scene/prefab YAML) — read each file in full before modifying it.
- No `Faction` enum, no per-`EnemyConfigSO` color field, no ally/companion tier — player-vs-enemy only, colors hardcoded on the two prefabs. Source: `docs/superpowers/specs/2026-07-27-entity-visual-contrast-design.md` Scope section.
- No changes to Hub scene, no changes to `EnemyConfigSO` combat fields.
- After every script change, request a compile via `mcp__UnityMCP__refresh_unity` (`compile: "request"`) and check `mcp__UnityMCP__read_console` for errors before moving on.
- Every new/renamed prefab or asset needs its `.meta` file — MCP tooling handles this automatically; do not create assets by hand.

---

### Task 1: `NightmareThemeSO` environment tint field

**Files:**
- Modify: `Assets/Scripts/Runtime/Data/NightmareThemeSO.cs`
- Test: `Assets/Tests/EditMode/NightmareThemeSOTests.cs` (create)

**Interfaces:**
- Produces: `NightmareThemeSO.EnvironmentTint` (`public Color EnvironmentTint => environmentTint;`), consumed by Task 2's `FloorInstantiator.ApplyEnvironmentTint`.

- [ ] **Step 1: Write the failing test**

Create `Assets/Tests/EditMode/NightmareThemeSOTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;
using Jagara.Runtime.Data;

namespace Jagara.Tests.EditMode
{
    public class NightmareThemeSOTests
    {
        private NightmareThemeSO theme;

        [SetUp]
        public void SetUp()
        {
            theme = ScriptableObject.CreateInstance<NightmareThemeSO>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(theme);
        }

        [Test]
        public void EnvironmentTint_DefaultsToDesaturatedDimTone()
        {
            var expected = new Color(140f / 255f, 140f / 255f, 153f / 255f, 1f);
            Assert.AreEqual(expected, theme.EnvironmentTint);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run via MCP: `mcp__UnityMCP__run_tests(mode="EditMode", test_names=["Jagara.Tests.EditMode.NightmareThemeSOTests.EnvironmentTint_DefaultsToDesaturatedDimTone"])`, then poll `get_test_job` until finished.
Expected: FAIL — `NightmareThemeSO` has no member `EnvironmentTint` (compile error).

- [ ] **Step 3: Write minimal implementation**

In `Assets/Scripts/Runtime/Data/NightmareThemeSO.cs`, add the field and property (below the existing `fog` field, before `enemyRoster`):

```csharp
        [SerializeField] private FogSettings fog = FogSettings.Default;

        [Header("Environment Tint")]
        [Tooltip("Multiply-tinted onto the floor and decoration Tilemaps to dim the environment relative to entities. White = no dimming.")]
        [SerializeField] private Color environmentTint = new Color(140f / 255f, 140f / 255f, 153f / 255f, 1f);

        [Header("Enemy Roster (weighted)")]
        [SerializeField] private List<WeightedEnemy> enemyRoster = new();

        public FogSettings Fog => fog;
        public Color EnvironmentTint => environmentTint;
        public IReadOnlyList<WeightedEnemy> EnemyRoster => enemyRoster;
```

- [ ] **Step 4: Run test to verify it passes**

Run: `mcp__UnityMCP__refresh_unity(compile: "request")`, then `mcp__UnityMCP__run_tests(mode="EditMode", test_names=["Jagara.Tests.EditMode.NightmareThemeSOTests.EnvironmentTint_DefaultsToDesaturatedDimTone"])`.
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Runtime/Data/NightmareThemeSO.cs Assets/Tests/EditMode/NightmareThemeSOTests.cs Assets/Tests/EditMode/NightmareThemeSOTests.cs.meta
git commit -m "feat: add environment tint field to NightmareThemeSO"
```

---

### Task 2: Apply environment tint to floor/decoration tilemaps

**Files:**
- Modify: `Assets/Scripts/Runtime/DungeonGen/FloorInstantiator.cs`
- Modify: `Assets/Scripts/Runtime/Gameplay/NightmareBootstrap.cs`
- Test: `Assets/Tests/EditMode/FloorInstantiatorEnvironmentTintTests.cs` (create)
- Test: `Assets/Tests/PlayMode/NightmareBootstrapTests.cs` (extend)

**Interfaces:**
- Consumes: `NightmareThemeSO.EnvironmentTint` (Task 1).
- Produces: `FloorInstantiator.DecorationTilemap` (`public Tilemap DecorationTilemap => decorationTilemap;`) and `FloorInstantiator.ApplyEnvironmentTint(NightmareThemeSO theme)` — both consumed by the Play Mode test in this task, `DecorationTilemap` potentially useful to later systems.

- [ ] **Step 1: Write the failing Edit Mode test**

Create `Assets/Tests/EditMode/FloorInstantiatorEnvironmentTintTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;
using Jagara.Runtime.Data;
using Jagara.Runtime.DungeonGen;

namespace Jagara.Tests.EditMode
{
    public class FloorInstantiatorEnvironmentTintTests
    {
        private GameObject instantiatorGO;
        private FloorInstantiator instantiator;
        private GameObject tilemapGO;
        private GameObject decorationTilemapGO;
        private NightmareThemeSO theme;

        [SetUp]
        public void SetUp()
        {
            instantiatorGO = new GameObject("FloorInstantiator");
            instantiator = instantiatorGO.AddComponent<FloorInstantiator>();

            tilemapGO = new GameObject("Tilemap");
            tilemapGO.AddComponent<Tilemap>();
            decorationTilemapGO = new GameObject("DecorationTilemap");
            decorationTilemapGO.AddComponent<Tilemap>();

            SetPrivateField(instantiator, "tilemap", tilemapGO.GetComponent<Tilemap>());
            SetPrivateField(instantiator, "decorationTilemap", decorationTilemapGO.GetComponent<Tilemap>());

            theme = ScriptableObject.CreateInstance<NightmareThemeSO>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(instantiatorGO);
            Object.DestroyImmediate(tilemapGO);
            Object.DestroyImmediate(decorationTilemapGO);
            Object.DestroyImmediate(theme);
        }

        [Test]
        public void ApplyEnvironmentTint_SetsColorOnBothTilemaps()
        {
            instantiator.ApplyEnvironmentTint(theme);

            Assert.AreEqual(theme.EnvironmentTint, instantiator.Tilemap.color);
            Assert.AreEqual(theme.EnvironmentTint, instantiator.DecorationTilemap.color);
        }

        [Test]
        public void ApplyEnvironmentTint_NullTheme_LeavesTilemapsUnchangedAndLogsError()
        {
            var before = instantiator.Tilemap.color;

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("theme is null"));
            instantiator.ApplyEnvironmentTint(null);

            Assert.AreEqual(before, instantiator.Tilemap.color);
        }

        [Test]
        public void ApplyEnvironmentTint_NoDecorationTilemap_StillTintsFloorTilemap()
        {
            SetPrivateField(instantiator, "decorationTilemap", null);

            instantiator.ApplyEnvironmentTint(theme);

            Assert.AreEqual(theme.EnvironmentTint, instantiator.Tilemap.color);
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

This test file needs `using UnityEngine.TestTools;` for `LogAssert` — add it to the `using` block above.

- [ ] **Step 2: Run test to verify it fails**

Run: `mcp__UnityMCP__run_tests(mode="EditMode", test_names=["Jagara.Tests.EditMode.FloorInstantiatorEnvironmentTintTests.ApplyEnvironmentTint_SetsColorOnBothTilemaps", "Jagara.Tests.EditMode.FloorInstantiatorEnvironmentTintTests.ApplyEnvironmentTint_NullTheme_LeavesTilemapsUnchangedAndLogsError", "Jagara.Tests.EditMode.FloorInstantiatorEnvironmentTintTests.ApplyEnvironmentTint_NoDecorationTilemap_StillTintsFloorTilemap"])`.
Expected: FAIL — `FloorInstantiator` has no `DecorationTilemap` property or `ApplyEnvironmentTint` method (compile error).

- [ ] **Step 3: Write minimal implementation**

In `Assets/Scripts/Runtime/DungeonGen/FloorInstantiator.cs`, add `DecorationTilemap` next to the existing `Tilemap` property (line 20):

```csharp
        public Tilemap Tilemap => tilemap;
        public Tilemap DecorationTilemap => decorationTilemap;
```

Then add `ApplyEnvironmentTint`, mirroring the existing null-check style used by `InstantiateFloor`. Insert it as a new public method after `InstantiateFloor` (after line 45's closing brace):

```csharp
        public void ApplyEnvironmentTint(NightmareThemeSO theme)
        {
            if (tilemap == null)
            {
                Debug.LogError("FloorInstantiator: Tilemap reference is not assigned.");
                return;
            }

            if (theme == null)
            {
                Debug.LogError("FloorInstantiator: theme is null; environment tint left unchanged.");
                return;
            }

            tilemap.color = theme.EnvironmentTint;
            if (decorationTilemap != null)
            {
                decorationTilemap.color = theme.EnvironmentTint;
            }
        }
```

Add `using Jagara.Runtime.Data;` if not already present (it already is, at line 1 — used for `DungeonTilesetSO`).

- [ ] **Step 4: Run test to verify it passes**

Run: `mcp__UnityMCP__refresh_unity(compile: "request")`, then re-run the same `run_tests` call from Step 2.
Expected: PASS on all three.

- [ ] **Step 5: Wire `NightmareBootstrap` to call it**

Read `Assets/Scripts/Runtime/Gameplay/NightmareBootstrap.cs` in full (already read during planning — line 43 is `floorInstantiator.InstantiateFloor(floor);`). Add the tint call immediately after it:

```csharp
            int seed = new System.Random().Next();
            FloorData floor = new DungeonGenerator().GenerateFloor(seed, generationParams.ToParams());
            floorInstantiator.InstantiateFloor(floor);
            floorInstantiator.ApplyEnvironmentTint(nightmareTheme);
            gridOverlayInstantiator?.RenderOverlay(floor);
```

`ApplyEnvironmentTint` already logs its own error if `nightmareTheme` is null (Step 3), matching how `ApplyFog` delegates its own null theme handling to `FogController.Apply` — no extra guard needed here.

- [ ] **Step 6: Write the failing Play Mode test**

Read `Assets/Tests/PlayMode/NightmareBootstrapTests.cs` in full (already read during planning). Add a constant next to `ParamsAssetPath` (line 18) and a new test method inside the class:

```csharp
        private const string ThemeAssetPath = "Assets/ScriptableObjects/Themes/DefaultNightmareTheme.asset";
```

```csharp
        [UnityTest]
        public IEnumerator NightmareBootstrap_OnStart_AppliesEnvironmentTintToBothTilemaps()
        {
            EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;

            var floorInstantiator = Object.FindFirstObjectByType<FloorInstantiator>();
            Assert.IsNotNull(floorInstantiator, "Expected a FloorInstantiator in the scene.");

            var theme = AssetDatabase.LoadAssetAtPath<NightmareThemeSO>(ThemeAssetPath);
            Assert.IsNotNull(theme, $"Expected a NightmareThemeSO asset at '{ThemeAssetPath}'.");

            Assert.AreEqual(theme.EnvironmentTint, floorInstantiator.Tilemap.color,
                "Expected the floor tilemap's color to match the theme's EnvironmentTint.");
            Assert.AreEqual(theme.EnvironmentTint, floorInstantiator.DecorationTilemap.color,
                "Expected the decoration tilemap's color to match the theme's EnvironmentTint.");
        }
```

- [ ] **Step 7: Run test to verify it fails, then passes**

Run: `mcp__UnityMCP__run_tests(mode="PlayMode", test_names=["Jagara.Tests.PlayMode.NightmareBootstrapTests.NightmareBootstrap_OnStart_AppliesEnvironmentTintToBothTilemaps"], init_timeout=120000)` — before Step 5's wiring this fails (mismatched colors, since nothing sets `tilemap.color` yet); confirm it now passes after Steps 3-6 are all in place.
Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add Assets/Scripts/Runtime/DungeonGen/FloorInstantiator.cs Assets/Scripts/Runtime/Gameplay/NightmareBootstrap.cs Assets/Tests/EditMode/FloorInstantiatorEnvironmentTintTests.cs Assets/Tests/EditMode/FloorInstantiatorEnvironmentTintTests.cs.meta Assets/Tests/PlayMode/NightmareBootstrapTests.cs
git commit -m "feat: apply theme environment tint to floor and decoration tilemaps"
```

---

### Task 3: `EntityFactionOutline` shader

**Files:**
- Create (via MCP): `Assets/Art/Shaders/EntityFactionOutline.shader`

**Interfaces:**
- Produces: shader `Jagara/EntityFactionOutline` with properties `_MainTex`, `_Color`, `_OutlineColor`, `_OutlineWidth`, `_Saturation`, `_Brightness` — consumed by Task 4's material and `EntityOutline` component (which drives `_OutlineColor` via `MaterialPropertyBlock`).

No automated test applies to shader code itself (HLSL isn't unit-testable in this project's Edit/Play Mode framework) — verification is a successful Editor compile with no console errors, confirmed in Step 2.

- [ ] **Step 1: Create the shader via MCP**

```
mcp__UnityMCP__manage_shader(
  action="create",
  name="EntityFactionOutline",
  path="Assets/Art/Shaders",
  contents=<the shader source below>
)
```

Shader source:

```hlsl
Shader "Jagara/EntityFactionOutline"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _OutlineColor ("Outline Color", Color) = (1, 1, 1, 1)
        _OutlineWidth ("Outline Width (texels)", Range(0, 4)) = 1
        _Saturation ("Saturation Multiplier", Range(0, 3)) = 1.25
        _Brightness ("Brightness Multiplier", Range(0, 3)) = 1.1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _MainTex_TexelSize;
                half4 _Color;
                half4 _OutlineColor;
                float _OutlineWidth;
                float _Saturation;
                float _Brightness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color * _Color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 baseSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * IN.color;

                if (baseSample.a > 0.001)
                {
                    half luma = dot(baseSample.rgb, half3(0.299, 0.587, 0.114));
                    half3 boosted = lerp(luma.xxx, baseSample.rgb, _Saturation) * _Brightness;
                    return half4(boosted, baseSample.a);
                }

                float2 texel = _MainTex_TexelSize.xy * _OutlineWidth;
                half neighborAlpha = 0;
                neighborAlpha += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(texel.x, 0)).a;
                neighborAlpha += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv - float2(texel.x, 0)).a;
                neighborAlpha += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(0, texel.y)).a;
                neighborAlpha += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv - float2(0, texel.y)).a;

                if (neighborAlpha > 0.001)
                {
                    return half4(_OutlineColor.rgb, _OutlineColor.a);
                }

                return half4(0, 0, 0, 0);
            }
            ENDHLSL
        }
    }
}
```

- [ ] **Step 2: Verify it compiles**

Run: `mcp__UnityMCP__refresh_unity(compile: "request")`, then `mcp__UnityMCP__read_console(action="get", types=["error"])`.
Expected: no errors referencing `EntityFactionOutline.shader`.

- [ ] **Step 3: Commit**

```bash
git add Assets/Art/Shaders/EntityFactionOutline.shader Assets/Art/Shaders/EntityFactionOutline.shader.meta
git commit -m "feat: add EntityFactionOutline sprite outline/boost shader"
```

---

### Task 4: `EntityOutline` component + shared material

**Files:**
- Create: `Assets/Scripts/Runtime/Gameplay/EntityOutline.cs`
- Create (via MCP): `Assets/Art/Materials/EntityFactionOutline.mat`
- Test: `Assets/Tests/EditMode/EntityOutlineTests.cs` (create)

**Interfaces:**
- Consumes: shader `Jagara/EntityFactionOutline` and its `_OutlineColor` property (Task 3).
- Produces: `EntityOutline` (`[RequireComponent(typeof(SpriteRenderer))]`, `[SerializeField] private Color outlineColor`) — consumed by Task 5's prefab wiring.

- [ ] **Step 1: Write the failing test**

Create `Assets/Tests/EditMode/EntityOutlineTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;
using Jagara.Runtime.Gameplay;

namespace Jagara.Tests.EditMode
{
    public class EntityOutlineTests
    {
        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");

        private GameObject go;

        [TearDown]
        public void TearDown()
        {
            if (go != null)
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Awake_WritesConfiguredOutlineColorIntoSpriteRendererPropertyBlock()
        {
            go = new GameObject("Entity");
            var spriteRenderer = go.AddComponent<SpriteRenderer>();
            var outline = go.AddComponent<EntityOutline>();

            var expected = new Color(1f, 0.231f, 0.231f, 1f);
            SetPrivateField(outline, "outlineColor", expected);
            InvokeAwake(outline);

            var block = new MaterialPropertyBlock();
            spriteRenderer.GetPropertyBlock(block);
            Assert.AreEqual(expected, block.GetColor(OutlineColorId));
        }

        private static void SetPrivateField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(field, $"field '{name}' not found");
            field.SetValue(target, value);
        }

        private static void InvokeAwake(MonoBehaviour behaviour)
        {
            var method = behaviour.GetType().GetMethod("Awake",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(method, "Awake method not found");
            method.Invoke(behaviour, null);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `mcp__UnityMCP__run_tests(mode="EditMode", test_names=["Jagara.Tests.EditMode.EntityOutlineTests.Awake_WritesConfiguredOutlineColorIntoSpriteRendererPropertyBlock"])`.
Expected: FAIL — `EntityOutline` does not exist (compile error).

- [ ] **Step 3: Write minimal implementation**

Create `Assets/Scripts/Runtime/Gameplay/EntityOutline.cs`:

```csharp
using UnityEngine;

namespace Jagara.Runtime.Gameplay
{
    /// <summary>
    /// Writes a per-instance outline color into the sprite's MaterialPropertyBlock
    /// so a single shared EntityFactionOutline material can render a different
    /// outline per entity without instancing the material (see GridVisualAnimator,
    /// which lives on the same "Visual" child GameObject, for the sibling pattern).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class EntityOutline : MonoBehaviour
    {
        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");

        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Color outlineColor = Color.white;

        private void Awake()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            var block = new MaterialPropertyBlock();
            spriteRenderer.GetPropertyBlock(block);
            block.SetColor(OutlineColorId, outlineColor);
            spriteRenderer.SetPropertyBlock(block);
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `mcp__UnityMCP__refresh_unity(compile: "request")`, then re-run Step 2's `run_tests` call.
Expected: PASS.

- [ ] **Step 5: Create the shared material via MCP**

```
mcp__UnityMCP__manage_material(
  action="create",
  material_path="Assets/Art/Materials/EntityFactionOutline.mat",
  shader="Jagara/EntityFactionOutline"
)
```

The shader's `Properties` block defaults (`_OutlineWidth=1`, `_Saturation=1.25`, `_Brightness=1.1`, `_OutlineColor=(1,1,1,1)`) populate automatically — no further property calls needed. Per-entity outline color is set at runtime by `EntityOutline`, not on the shared material.

- [ ] **Step 6: Verify with `get_material_info`**

```
mcp__UnityMCP__manage_material(action="get_material_info", material_path="Assets/Art/Materials/EntityFactionOutline.mat")
```

Expected: reports shader `Jagara/EntityFactionOutline` and the four custom properties with their default values.

- [ ] **Step 7: Commit**

```bash
git add Assets/Scripts/Runtime/Gameplay/EntityOutline.cs Assets/Tests/EditMode/EntityOutlineTests.cs Assets/Tests/EditMode/EntityOutlineTests.cs.meta Assets/Art/Materials/EntityFactionOutline.mat Assets/Art/Materials/EntityFactionOutline.mat.meta
git commit -m "feat: add EntityOutline component and shared EntityFactionOutline material"
```

---

### Task 5: Wire outline onto Player and Enemy prefabs

**Files:**
- Modify (via MCP, interactive prefab editing): `Assets/Prefabs/Player.prefab`
- Modify (via MCP, interactive prefab editing): `Assets/Prefabs/Enemy.prefab`
- Test: `Assets/Tests/PlayMode/NightmareBootstrapTests.cs` (extend)

**Interfaces:**
- Consumes: `EntityOutline` component and `EntityFactionOutline.mat` (Task 4).
- Both prefabs already have a child GameObject named `Visual` holding the `SpriteRenderer` and `GridVisualAnimator` (confirmed by reading both `.prefab` files during planning) — `EntityOutline` and the material swap both go on that same child.

Color values: Player = `#E8B84B` → `(0.9098039, 0.7215686, 0.2941176, 1)`. Enemy = `#FF3B3B` → `(1.0, 0.2313725, 0.2313725, 1)`. These are `232/255`, `184/255`, `75/255` and `59/255` carried to 7 decimal places — use exactly these digits in both the MCP `set_property` calls (Steps 1-2) and the test constants (Step 3) so no float-rounding mismatch causes a spurious test failure.

- [ ] **Step 1: Edit Player.prefab**

```
mcp__UnityMCP__manage_prefabs(action="open_prefab_stage", prefab_path="Assets/Prefabs/Player.prefab")
```

```
mcp__UnityMCP__manage_components(
  action="add",
  target="Visual",
  search_method="by_name",
  component_type="Jagara.Runtime.Gameplay.EntityOutline"
)
```

```
mcp__UnityMCP__manage_components(
  action="set_property",
  target="Visual",
  search_method="by_name",
  component_type="EntityOutline",
  property="outlineColor",
  value={"r": 0.9098039, "g": 0.7215686, "b": 0.2941176, "a": 1.0}
)
```

```
mcp__UnityMCP__manage_material(
  action="assign_material_to_renderer",
  target="Visual",
  search_method="by_name",
  material_path="Assets/Art/Materials/EntityFactionOutline.mat"
)
```

```
mcp__UnityMCP__manage_prefabs(action="save_prefab_stage")
mcp__UnityMCP__manage_prefabs(action="close_prefab_stage")
```

- [ ] **Step 2: Edit Enemy.prefab**

```
mcp__UnityMCP__manage_prefabs(action="open_prefab_stage", prefab_path="Assets/Prefabs/Enemy.prefab")
```

```
mcp__UnityMCP__manage_components(
  action="add",
  target="Visual",
  search_method="by_name",
  component_type="Jagara.Runtime.Gameplay.EntityOutline"
)
```

```
mcp__UnityMCP__manage_components(
  action="set_property",
  target="Visual",
  search_method="by_name",
  component_type="EntityOutline",
  property="outlineColor",
  value={"r": 1.0, "g": 0.2313725, "b": 0.2313725, "a": 1.0}
)
```

```
mcp__UnityMCP__manage_material(
  action="assign_material_to_renderer",
  target="Visual",
  search_method="by_name",
  material_path="Assets/Art/Materials/EntityFactionOutline.mat"
)
```

```
mcp__UnityMCP__manage_prefabs(action="save_prefab_stage")
mcp__UnityMCP__manage_prefabs(action="close_prefab_stage")
```

If `search_method="by_name"` with `target="Visual"` doesn't resolve inside the open prefab stage (ambiguous if any other object nearby is also named "Visual"), use `mcp__UnityMCP__manage_prefabs(action="get_hierarchy", prefab_path=<path>)` first to confirm the exact path/instance ID of the `Visual` child, then retarget with `search_method="by_path"` or the instance ID.

- [ ] **Step 3: Write the failing Play Mode test**

Read `Assets/Tests/PlayMode/NightmareBootstrapTests.cs` in full (state after Task 2's edits). Add `using Jagara.Runtime.Gameplay;` to its `using` block, then add:

```csharp
        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly Color PlayerOutlineColor = new Color(232f / 255f, 184f / 255f, 75f / 255f, 1f);
        private static readonly Color EnemyOutlineColor = new Color(1f, 59f / 255f, 59f / 255f, 1f);

        [UnityTest]
        public IEnumerator NightmareBootstrap_OnStart_SpawnsPlayerAndEnemiesWithFactionOutlineColors()
        {
            EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;

            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.IsNotNull(player, "Expected a PlayerController in the scene.");
            AssertOutlineColor(player.transform, PlayerOutlineColor, "Player");

            var enemies = Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
            Assert.Greater(enemies.Length, 0, "Expected at least one spawned enemy.");
            foreach (var enemy in enemies)
            {
                AssertOutlineColor(enemy.transform, EnemyOutlineColor, "Enemy");
            }
        }

        private static void AssertOutlineColor(Transform root, Color expected, string label)
        {
            var spriteRenderer = root.GetComponentInChildren<SpriteRenderer>();
            Assert.IsNotNull(spriteRenderer, $"Expected a SpriteRenderer under {label}.");

            var block = new MaterialPropertyBlock();
            spriteRenderer.GetPropertyBlock(block);
            Color actual = block.GetColor(OutlineColorId);
            Assert.AreEqual(expected, actual, $"{label} outline color mismatch.");
        }
```

- [ ] **Step 4: Run test to verify it fails, then passes**

Run: `mcp__UnityMCP__run_tests(mode="PlayMode", test_names=["Jagara.Tests.PlayMode.NightmareBootstrapTests.NightmareBootstrap_OnStart_SpawnsPlayerAndEnemiesWithFactionOutlineColors"], init_timeout=120000)` before Steps 1-2 (fails — no `EntityOutline` on either prefab yet, property block color is the shader default white instead of gold/red) and again after (passes).

- [ ] **Step 5: Commit**

```bash
git add Assets/Prefabs/Player.prefab Assets/Prefabs/Enemy.prefab Assets/Tests/PlayMode/NightmareBootstrapTests.cs
git commit -m "feat: wire faction outline colors onto Player and Enemy prefabs"
```

---

### Task 6: Manual visual verification

**Files:** none (no code changes) — this task confirms the finished feature against the original problem.

- [ ] **Step 1: Run the full test suite**

```
mcp__UnityMCP__run_tests(mode="EditMode")
mcp__UnityMCP__run_tests(mode="PlayMode", init_timeout=120000)
```

Expected: all tests pass, including the five new ones added across Tasks 1, 2, 4, and 5.

- [ ] **Step 2: Enter Play Mode and look at it**

```
mcp__UnityMCP__manage_editor(action="play")
```

Ask the user to open the `DungeonPreview` scene's Game view and compare against the original screenshot's scenario (an enemy blob, the player, a second enemy, and root/vine decoration together in frame): confirm the player reads as gold-outlined, enemies read as red-outlined, and the floor/decoration read visibly dimmer than the entities.

```
mcp__UnityMCP__manage_editor(action="stop")
```

If the outline looks blurry/soft instead of crisp pixel-art, check the sprite's texture import **Filter Mode** — it should be `Point (no filter)` for the neighbor-alpha-sampling outline to produce a sharp edge; this plan does not change texture import settings, so flag it to the user as a separate follow-up if it's set to `Bilinear`.

- [ ] **Step 3: Final commit (if anything was adjusted)**

Only if Step 2 surfaced a fix (e.g. a tweaked default `_OutlineWidth`/`_Saturation`/`_Brightness` on the material, done via `mcp__UnityMCP__manage_material(action="set_material_shader_property", ...)`):

```bash
git add Assets/Art/Materials/EntityFactionOutline.mat
git commit -m "fix: tune outline/boost defaults after visual check"
```

**Outcome (2026-07-27):** Step 2's manual check found the hard-edge outline rejected outright by the user ("todo el enfoque no funciona") as too harsh on the pixel art. A follow-up round of visual mockups (soft glow / ground marker / corner badge / soft sprite-wide tint) was reviewed; **soft glow, very subtle, no sprite-wide saturation/brightness boost** was chosen. See the spec's "Revision 2026-07-27" section and Task 7 below.

---

### Task 7: Soften the outline into a subtle glow

**Files:**
- Modify (via MCP `manage_shader` update): `Assets/Art/Shaders/EntityFactionOutline.shader`
- Modify (via MCP `manage_material`): `Assets/Art/Materials/EntityFactionOutline.mat` (new property defaults)

**Interfaces:**
- No change to `EntityOutline.cs`, the `_OutlineColor` `MaterialPropertyBlock` mechanism, the material's asset path, or prefab wiring (Tasks 4-5) — this task only changes the shader's rendering technique and two material-level tuning defaults. `EntityOutline` keeps writing `_OutlineColor` exactly as before; the shader just renders it as a soft glow instead of a hard ring, and no longer boosts the sprite's own saturation/brightness.
- Removes shader properties `_Saturation` and `_Brightness` (no longer used — the sprite renders untouched); adds `_GlowIntensity`. `_OutlineWidth` is repurposed from "outline ring width in texels" to "glow radius in texels" (same property name, new meaning, new default).

Per the design spec, no automated test applies to shader code in this project — verification is a clean compile plus manual visual confirmation, same as Task 3 and Task 6.

- [ ] **Step 1: Update the shader via MCP**

```
mcp__UnityMCP__manage_shader(
  action="update",
  name="EntityFactionOutline",
  path="Assets/Art/Shaders",
  contents=<the shader source below>
)
```

Shader source (replaces the Task 3 version in full):

```hlsl
Shader "Jagara/EntityFactionOutline"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _OutlineColor ("Glow Color", Color) = (1, 1, 1, 1)
        _OutlineWidth ("Glow Radius (texels)", Range(1, 4)) = 2
        _GlowIntensity ("Glow Intensity", Range(0, 1)) = 0.3
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _MainTex_TexelSize;
                half4 _Color;
                half4 _OutlineColor;
                float _OutlineWidth;
                float _GlowIntensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color * _Color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 baseSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * IN.color;

                if (baseSample.a > 0.001)
                {
                    return baseSample;
                }

                static const int MAX_RINGS = 4;
                float2 texel = _MainTex_TexelSize.xy;
                half glow = 0;

                [unroll]
                for (int r = 1; r <= MAX_RINGS; r++)
                {
                    if (r > (int)_OutlineWidth)
                    {
                        break;
                    }

                    float2 offset = texel * r;
                    half ringAlpha = 0;
                    ringAlpha = max(ringAlpha, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(offset.x, 0)).a);
                    ringAlpha = max(ringAlpha, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv - float2(offset.x, 0)).a);
                    ringAlpha = max(ringAlpha, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(0, offset.y)).a);
                    ringAlpha = max(ringAlpha, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv - float2(0, offset.y)).a);
                    ringAlpha = max(ringAlpha, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(offset.x, offset.y)).a);
                    ringAlpha = max(ringAlpha, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv - float2(offset.x, offset.y)).a);
                    ringAlpha = max(ringAlpha, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(offset.x, -offset.y)).a);
                    ringAlpha = max(ringAlpha, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(-offset.x, offset.y)).a);

                    half falloff = 1.0 - (half)(r - 1) / (half)_OutlineWidth;
                    glow = max(glow, ringAlpha * falloff);
                }

                glow *= _GlowIntensity;
                return half4(_OutlineColor.rgb, glow);
            }
            ENDHLSL
        }
    }
}
```

- [ ] **Step 2: Verify it compiles**

Run: `mcp__UnityMCP__refresh_unity(compile: "request")`, then `mcp__UnityMCP__read_console(action="get", types=["error"])`.
Expected: no errors referencing `EntityFactionOutline.shader`.

- [ ] **Step 3: Confirm the material picked up the new properties**

```
mcp__UnityMCP__manage_material(action="get_material_info", material_path="Assets/Art/Materials/EntityFactionOutline.mat")
```

Expected: `_OutlineWidth` and `_GlowIntensity` present with the shader's new defaults (`2`, `0.3`); `_Saturation`/`_Brightness` no longer listed (removed from the shader). `_OutlineColor` keeps whatever per-instance runtime value `EntityOutline` writes — the material-level default (`1,1,1,1`) is only what an unwired renderer would show.

If the material still shows stale `_Saturation`/`_Brightness` values after the shader update, force a resync: `mcp__UnityMCP__manage_material(action="set_material_shader_property", material_path="Assets/Art/Materials/EntityFactionOutline.mat", property="_GlowIntensity", value=0.3)` (harmless no-op if already synced, forces Unity to refresh the material's property list against the shader).

- [ ] **Step 4: Run the full test suite**

```
mcp__UnityMCP__run_tests(mode="EditMode")
mcp__UnityMCP__run_tests(mode="PlayMode", init_timeout=120000)
```

Expected: all tests still pass — this task doesn't touch `EntityOutline.cs`, `_OutlineColor`, or anything the existing Edit/Play Mode tests assert on, so no test changes are expected or needed.

- [ ] **Step 5: Commit**

```bash
git add Assets/Art/Shaders/EntityFactionOutline.shader Assets/Art/Materials/EntityFactionOutline.mat
git commit -m "fix: replace hard outline with a subtle glow per user feedback"
```

- [ ] **Step 6: Manual visual re-check**

```
mcp__UnityMCP__manage_editor(action="play")
```

Ask the user to look again and confirm the glow now reads as subtle/faint rather than a hard ring, and that the sprite itself looks untouched (no saturation/brightness shift).

```
mcp__UnityMCP__manage_editor(action="stop")
```

If the user asks for it fainter/stronger/wider, retune via `mcp__UnityMCP__manage_material(action="set_material_shader_property", ...)` on `_GlowIntensity` (0-1) or `_OutlineWidth` (1-4) — no code change needed, and re-run Step 6.

**Outcome (2026-07-27):** Step 6's re-check found the soft glow rejected too — at this project's native pixel-art resolution with point filtering, the multi-ring falloff only has 2-4 discrete distance steps to work with, which rendered as a blocky gradient patch rather than a soft aura ("no parece un glow... parece más una textura con gradient"). Presented with this root cause, the user chose to revert to a **solid 1px hard outline** (Task 3's original technique) but with **muted/desaturated colors** instead of the original bright gold/red. See the spec's second "Revision 2026-07-27" section and Task 8 below.

---

### Task 8: Revert to a solid outline with muted colors

**Files:**
- Modify (via MCP `manage_shader` update): `Assets/Art/Shaders/EntityFactionOutline.shader`
- Modify (via MCP `manage_material`): `Assets/Art/Materials/EntityFactionOutline.mat` (drop stale glow properties, reset `_OutlineWidth` default)
- Modify (via MCP, interactive prefab editing): `Assets/Prefabs/Player.prefab`, `Assets/Prefabs/Enemy.prefab` (new muted `outlineColor` values)
- Modify: `Assets/Tests/PlayMode/NightmareBootstrapTests.cs` (update `PlayerOutlineColor`/`EnemyOutlineColor` constants to match)

**Interfaces:**
- No change to `EntityOutline.cs` or its `_OutlineColor` `MaterialPropertyBlock` mechanism — only the shader's rendering technique (back to a hard single-ring check, no glow falloff) and the two prefabs' stored `outlineColor` values change.
- Removes `_GlowIntensity` (no longer used); `_OutlineWidth` reverts to meaning "outline ring width in texels" (Task 3's original meaning), default `1`.
- New muted colors: Player `#C2A874` → `(0.7607843, 0.6588235, 0.4549020, 1)` (a tan-gold, `194/255, 168/255, 116/255`). Enemy `#A85C52` → `(0.6588235, 0.3607843, 0.3215686, 1)` (a dusty brick-red, `168/255, 92/255, 82/255`). Use these exact 7-decimal values in both the prefab `set_property` calls and the test constants — the existing tolerance-based `AssertOutlineColor` (from Task 5's fix) means exact bit-matching isn't required, but keeping them consistent avoids confusion.

- [ ] **Step 1: Update the shader via MCP**

```
mcp__UnityMCP__manage_shader(
  action="update",
  name="EntityFactionOutline",
  path="Assets/Art/Shaders",
  contents=<the shader source below>
)
```

Shader source (replaces the Task 7 version in full — reverts to Task 3's hard-outline fragment logic, keeps Task 7's "no sprite boost" behavior on the opaque path):

```hlsl
Shader "Jagara/EntityFactionOutline"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _OutlineColor ("Outline Color", Color) = (1, 1, 1, 1)
        _OutlineWidth ("Outline Width (texels)", Range(0, 4)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _MainTex_TexelSize;
                half4 _Color;
                half4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color * _Color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 baseSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * IN.color;

                if (baseSample.a > 0.001)
                {
                    return baseSample;
                }

                float2 texel = _MainTex_TexelSize.xy * _OutlineWidth;
                half neighborAlpha = 0;
                neighborAlpha += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(texel.x, 0)).a;
                neighborAlpha += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv - float2(texel.x, 0)).a;
                neighborAlpha += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(0, texel.y)).a;
                neighborAlpha += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv - float2(0, texel.y)).a;

                if (neighborAlpha > 0.001)
                {
                    return half4(_OutlineColor.rgb, _OutlineColor.a);
                }

                return half4(0, 0, 0, 0);
            }
            ENDHLSL
        }
    }
}
```

- [ ] **Step 2: Verify it compiles**

Run: `mcp__UnityMCP__refresh_unity(compile: "request")`, then `mcp__UnityMCP__read_console(action="get", types=["error"])`.
Expected: no errors referencing `EntityFactionOutline.shader`.

- [ ] **Step 3: Confirm the material dropped the stale glow property**

```
mcp__UnityMCP__manage_material(action="get_material_info", material_path="Assets/Art/Materials/EntityFactionOutline.mat")
```

Expected: `_OutlineWidth` present (default `1`); `_GlowIntensity` no longer listed. If it's still listed with a stale value, force a resync via `mcp__UnityMCP__manage_material(action="set_material_shader_property", material_path="Assets/Art/Materials/EntityFactionOutline.mat", property="_OutlineWidth", value=1)`.

- [ ] **Step 4: Update both prefabs' outline colors**

Player.prefab:
```
mcp__UnityMCP__manage_prefabs(action="open_prefab_stage", prefab_path="Assets/Prefabs/Player.prefab")
mcp__UnityMCP__manage_components(action="set_property", target="Visual", search_method="by_name", component_type="EntityOutline", property="outlineColor", value={"r": 0.7607843, "g": 0.6588235, "b": 0.4549020, "a": 1.0})
mcp__UnityMCP__manage_prefabs(action="save_prefab_stage")
mcp__UnityMCP__manage_prefabs(action="close_prefab_stage")
```

Enemy.prefab:
```
mcp__UnityMCP__manage_prefabs(action="open_prefab_stage", prefab_path="Assets/Prefabs/Enemy.prefab")
mcp__UnityMCP__manage_components(action="set_property", target="Visual", search_method="by_name", component_type="EntityOutline", property="outlineColor", value={"r": 0.6588235, "g": 0.3607843, "b": 0.3215686, "a": 1.0})
mcp__UnityMCP__manage_prefabs(action="save_prefab_stage")
mcp__UnityMCP__manage_prefabs(action="close_prefab_stage")
```

(Both prefabs already have `EntityOutline` on their `Visual` child from Task 5 — this only updates the color value, no `add`/material-assignment needed again.)

- [ ] **Step 5: Update the Play Mode test's color constants**

In `Assets/Tests/PlayMode/NightmareBootstrapTests.cs`, update the constants added in Task 5:

```csharp
        private static readonly Color PlayerOutlineColor = new Color(194f / 255f, 168f / 255f, 116f / 255f, 1f);
        private static readonly Color EnemyOutlineColor = new Color(168f / 255f, 92f / 255f, 82f / 255f, 1f);
```

The rest of `NightmareBootstrap_OnStart_SpawnsPlayerAndEnemiesWithFactionOutlineColors` and `AssertOutlineColor` (the tolerance-based comparison from Task 5's fix) stay unchanged.

- [ ] **Step 6: Run the full test suite**

```
mcp__UnityMCP__run_tests(mode="EditMode")
mcp__UnityMCP__run_tests(mode="PlayMode", init_timeout=120000)
```

Expected: all tests pass, including the updated outline-color assertion.

- [ ] **Step 7: Commit**

```bash
git add Assets/Art/Shaders/EntityFactionOutline.shader Assets/Art/Materials/EntityFactionOutline.mat Assets/Prefabs/Player.prefab Assets/Prefabs/Enemy.prefab Assets/Tests/PlayMode/NightmareBootstrapTests.cs
git commit -m "fix: revert glow to a solid muted outline per user feedback"
```

- [ ] **Step 8: Manual visual re-check**

```
mcp__UnityMCP__manage_editor(action="play")
```

Ask the user to look again and confirm the muted solid outline reads well now. Leave Play Mode running for the controller/user to check immediately rather than calling `stop` — this mirrors Task 7's approach, since the last two rounds both needed a live look before either being satisfied or triggering another iteration.

---

## Self-Review Notes

- **Spec coverage:** Task 1-2 implement the spec's "Environment dimming" section in full (theme field, both tilemaps, `NightmareBootstrap` wiring, edge cases for null theme/missing decoration tilemap). Tasks 3-5 implement "Entity outline shader" in full (shader, component, material, prefab wiring, hardcoded per-prefab colors, no `Faction` enum). Task 6 covers the spec's "Testing" section's manual verification requirement. Out-of-scope items (ally tier, ground marker, Hub scene, `EnemyConfigSO` combat fields) are untouched, matching the spec's explicit deferrals.
- **Type consistency:** `EnvironmentTint`, `DecorationTilemap`, `ApplyEnvironmentTint(NightmareThemeSO)`, and `EntityOutline`/`outlineColor`/`_OutlineColor` are named identically everywhere they're referenced across tasks.
- **Refinement beyond the spec:** the spec described the dimming mechanism as `TilemapRenderer.color`; this plan uses `Tilemap.color` instead (the actual native Unity API for multiply-tinting a tilemap's tiles) — same effect, simpler, no `GetComponent<TilemapRenderer>()` indirection needed since `FloorInstantiator` already holds direct `Tilemap` references. The spec's Edit Mode Test guidance ("not Edit Mode Test territory") is also refined per-task: `NightmareThemeSO` defaults, `FloorInstantiator.ApplyEnvironmentTint`, and `EntityOutline`'s property-block write are all pure enough to Edit-Mode-test using this codebase's established `AddComponent`/reflection pattern (see `GridMoverTests.cs`, `DungeonTilesetSOTests.cs`); only the actual "does it look right" visual judgment (Task 6) remains manual, consistent with the spec's intent.
