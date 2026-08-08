# Pixel-Art Particle Redesign — Hit Spark & Torch Embers

Status: approved
Date: 2026-08-09

## Problem

`Assets/Art/Materials/HitSparkParticle.mat` and `Assets/Art/Materials/TorchEmberParticle.mat`
both reference Unity's built-in `Default-Particle` texture — a smooth, anti-aliased radial
gradient. Against JĀGARA's hard-edged, Point-filtered, low-resolution pixel art (16px tileset,
`filterMode: 0` throughout), that soft circular blob reads as an engine default, not a pixel-art
hit spark or ember. This spec replaces it with a hand-authored, hard-edged pixel sprite shared by
both materials.

Explored and rejected during brainstorming: a runtime pixelation shader over the existing soft
circle (unnecessary complexity, no precedent in the project's shader folder), and reusing an icon
from `Assets/Art/The Roguelike 1-16-1 Alpha.png` (those are full-color, detailed 16×16 icons —
tinting one by a particle's randomized start-color would look wrong, and they're too busy at
burst scale).

## Design

### 1. New shared texture: `Assets/Art/Materials/PixelSpark.png`

A 7×7 canvas, transparent background, flat white 3×3 cross centered at (3,3):

```
. . . . . . .
. . . . . . .
. . . # . . .
. . # # # . .
. . . # . . .
. . . . . . .
. . . . . . .
```

Hard 1-bit alpha — fully opaque white or fully transparent, no anti-aliasing — matching the rest
of the project's pixel art. Flat white (not pre-tinted) so each `ParticleSystem`'s own start-color
still drives the final color: `HitParticles` (constant white) and `TorchFlame` (random
orange↔yellow gradient) both keep working unmodified.

Authored pixel-exact via a short Unity Editor script (`Texture2D.SetPixel` + `EncodeToPNG`) run
through MCP's `execute_code`, not AI image generation — this shape needs exact 1-bit pixel edges,
not an approximation.

Import settings: Texture Type = Default, Filter Mode = Point, Wrap Mode = Clamp, Compression =
None (avoids blur/artifacts on a texture this small).

### 2. Material changes

`HitSparkParticle.mat` and `TorchEmberParticle.mat` both get `_BaseMap` repointed from the
built-in `Default-Particle` to `PixelSpark.png`. No other material property changes — shader
(`Universal Render Pipeline/Particles/Unlit`), Surface Type (Transparent), Blend Mode (Additive),
and base color (white) all stay as-is; they already read well against the dark dungeon background.

### 3. ParticleSystem tuning — none

Checked the actual tuned values on `Assets/Prefabs/Player.prefab` (not the draft numbers in the
original `2026-08-09-hit-particles-design.md`): `HitParticles` Start Size is a constant `0.25`,
burst count is `10–15` (random range), `TorchFlame` Start Size randomizes `0.1–0.3`. Both are
large enough that the new hard-edged 3×3 texture renders as a clearly legible multi-screen-pixel
chunky cross rather than disappearing — no numeric changes to either `ParticleSystem`.

Explicit non-change: neither system randomizes rotation today, and this stays off. A rotated,
Point-filtered 3×3 sprite would look mushy/aliased at this scale instead of staying grid-crisp.

### 4. Mechanics

- Texture created and saved via MCP `execute_code`.
- Texture import settings applied via MCP asset/texture tools.
- Material `_BaseMap` reassignment done via MCP material tools — never by hand-editing `.mat`
  YAML.

## Out of scope

- No changes to burst timing, counts, lifetime curves, or color-over-lifetime curves on either
  `ParticleSystem`.
- No changes to `CombatResolver`, `HealthState`, `TorchFlameToggle`, or `GridVisualAnimator`.
- No enemy-side hit particles — stays player-only per the narrowing already recorded in
  `2026-08-09-hit-particles-design.md`.
- No new shader — both materials keep `Universal Render Pipeline/Particles/Unlit`.

## Testing

Manual Play Mode verification via MCP:

1. Player takes a hit: burst reads as crisp white cross-shaped chunks, not a soft blob.
2. Torch embers read as crisp orange/yellow chunks instead of a soft glow.
3. `read_console` shows no new errors or warnings (in particular, no pink/magenta
   missing-shader fallback).
