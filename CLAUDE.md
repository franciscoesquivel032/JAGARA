# CLAUDE.md — JĀGARA (Unity Project Guidelines)

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

> Design source of truth: `JĀGARA_GDD.docx`. Sections are tagged `[DEFINIDO]`, `[PROVISIONAL]`, or `[PENDIENTE]`. Treat `[DEFINIDO]` as binding, `[PROVISIONAL]` as the current best guess (implement it, but flag if it turns out awkward in code), and never silently invent behavior for a `[PENDIENTE]` item — implement the smallest reasonable placeholder and call it out explicitly instead of guessing the final design.

---

## Project state

JĀGARA is a fresh Unity project scaffold — currently just the default Unity 2D template, no gameplay code written yet. There is no existing architecture to conform to beyond the guidelines in this file — create the folder structure described below (`Assets/Scripts`, `Assets/Prefabs`, etc.) as needed unless the user directs otherwise.

JĀGARA is a narrative roguelite: turn-based grid exploration (Pokémon Mystery Dungeon–style) set inside procedurally generated nightmares, framed by a persistent hub ("the night before sleep") where the player shops, banks items/money, and spends stat points. See the GDD for full context — this file only covers implementation conventions.

## Engine and packages

- Unity **6000.5.3f1** (Unity 6).
- Render pipeline: **Universal Render Pipeline (URP)**, 2D renderer.
- Input: **Unity Input System**, not the legacy Input Manager.
- Expected/likely packages (verify against `Packages/manifest.json` before assuming any of these are present — this is a fresh scaffold and not all may be installed yet):
  - `com.coplaydev.unity-mcp` (MCP for Unity) — if connected in a session, prefer its tools for Editor-side operations (scene/asset inspection, play-mode control) over ad hoc file edits.
  - `com.unity.2d.*` (animation, aseprite, psdimporter, spriteshape, tilemap, tilemap.extras) — 2D art/animation pipeline; tilemap packages are directly relevant to dungeon rendering.
  - `com.unity.test-framework`.
- If a package this file assumes is missing when you actually need it, say so and ask before adding it — don't silently modify `manifest.json` for anything beyond what the task requires.

## Working with this repo

- This is a Unity project: **do not hand-edit `.meta` files, `Library/`, `Temp/`, `Logs/`, or `UserSettings/`** — these are generated/managed by the Unity Editor and are gitignored (except `.meta` files, which are tracked and must accompany any new asset).
- `.unity` scene files and `.asset` files are YAML; when they must be edited outside the Editor, make minimal targeted edits — large diffs are usually a sign the Editor (or MCP) should be doing the editing instead.
- No build/lint/test tooling is configured yet. Unity Test Framework is installed but no test assemblies exist yet — if adding tests, they run via Unity's Test Runner (Window > General > Test Runner) or `Unity.exe -runTests -projectPath . -testResults <path>` from the CLI.
- If Git LFS is configured, large binary assets (textures, audio, etc.) should go through LFS; check `.gitattributes` for tracked patterns before adding large files. If it isn't configured yet and the task adds large binaries, flag this rather than committing them raw.

---

## Core architecture: turn-based grid loop

This is the system most other systems hang off of, so get the contract right early.

- The dungeon is a grid of tiles. Each tile may hold at most one entity and, independently, one item (an entity can stand on a tile that also has an item — the item is picked up automatically on entry, per the GDD).
- Turn resolution: the player commits one action (move / attack / use item) → that action resolves → every other active entity on the floor takes its turn → control returns to the player. This must be a strict, ordered pipeline, not events firing in arbitrary order.
- Model entities that act on a turn behind a shared interface (e.g. `ITurnActor` with something like `TakeTurn()`), so the turn resolver doesn't need to know whether it's driving the player, an enemy, or a future companion.
- Avoid a god-object `GameManager.Instance` driving turns, combat, and UI all at once. Split into focused, narrowly-scoped classes (turn resolution, combat resolution, floor state) that communicate via the SO event pattern below — the turn loop is exactly the kind of central system that's tempting to make a monolith, resist that.
- Player actions per turn are exactly three per the GDD: move, attack, use item. Don't add a fourth action type without flagging that it's a design change, not just an implementation detail.

## Core architecture: procedural dungeon generation

- Each floor generates: room layout + connecting paths, item placement, staircase position, and enemy count/positions (bounded by floor depth — never unbounded random counts).
- Keep the generator itself deterministic given a seed (`DungeonGenerator.Generate(seed, depthParams) -> FloorData`), even though seeds themselves are randomized at runtime. This makes generation bugs reproducible and enables Edit Mode tests (see Testing below).
- Separate **generation** (producing a `FloorData` description: rooms, tile types, item/enemy spawn points) from **instantiation** (spawning actual GameObjects/tilemap tiles from that description). This split is what makes the generator testable without the Unity Editor lifecycle.
- Difficulty scaling per the GDD comes from floor count and map/enemy-count scaling — not enemy stat inflation or new enemy abilities per floor. Don't route "make it harder" requests through enemy ability additions; route them through the generator's depth parameters.

## Architecture: ScriptableObjects as data containers

- `ScriptableObject`s are shared data containers, not just static config. Expected examples for JĀGARA: `CharacterStatsSO`, `AbilitySO` (name, PP max, damage, range), `ItemSO`, `EnemyConfigSO`, `NightmareThemeSO`.
- When data needs to be shared across multiple GameObjects/scenes without coupling them together (player HP, paranoia level, bank contents), implement it as a SO instead of a singleton or static field.
- MonoBehaviours reference SOs via `[SerializeField]` — never load them at runtime with `Resources.Load` unless strictly necessary.
- **Runtime Set pattern**: for dynamic collections (e.g. list of active enemies on the current floor), use a `RuntimeSet<T>` SO that objects register/unregister themselves to in `OnEnable`/`OnDisable`.
- Core reusable runtime values as `FloatVariableSO` / `IntVariableSO`, each with a runtime value and a default to reset between Play Mode sessions. JĀGARA-specific ones to expect: `CurrentHP`, `MaxHP`, `ParanoiaLevel`, `MaxParanoia`, per-ability `CurrentPP`.

```csharp
[CreateAssetMenu(fileName = "New Float Variable", menuName = "Variables/Float")]
public class FloatVariableSO : ScriptableObject
{
    [SerializeField] private float initialValue;
    [System.NonSerialized] public float RuntimeValue;

    private void OnEnable() => RuntimeValue = initialValue;
}
```

## Architecture: events with ScriptableObjects

- Communication between decoupled systems (UI ↔ Gameplay, Audio ↔ Gameplay, TurnResolver ↔ everything) uses **ScriptableObject-based events** (Observer pattern), not direct references between MonoBehaviours or global singletons.
- Each event is an asset (`GameEventSO`) that any script can invoke (`Raise()`) or listen to (`GameEventListener` component or code subscription). Expected JĀGARA events: `OnParanoiaChanged`, `OnHPChanged`, `OnFloorCleared`, `OnNightmareSucceeded`, `OnNightmareFailed`, `OnPPDepleted`.
- For events carrying data, use typed variants (`IntGameEventSO`, `FloatGameEventSO`, etc.) instead of one generic event with `object`.

```csharp
[CreateAssetMenu(fileName = "New Game Event", menuName = "Events/Game Event")]
public class GameEventSO : ScriptableObject
{
    private readonly List<GameEventListener> listeners = new();

    public void Raise()
    {
        for (int i = listeners.Count - 1; i >= 0; i--)
            listeners[i].OnEventRaised();
    }

    public void RegisterListener(GameEventListener listener) => listeners.Add(listener);
    public void UnregisterListener(GameEventListener listener) => listeners.Remove(listener);
}
```

- **Plain C# events (Action)**: use for communication *within* a single system or class hierarchy (e.g. a component notifying its own controller). Reserve SO events for communication *between* systems that shouldn't know about each other — e.g. the turn resolver shouldn't hold a reference to the UI health bar.
- Initialize every C# event with `= delegate { };` to avoid constant null checks:

```csharp
public event Action OnHealthDepleted = delegate { };
```

- Always unsubscribe in `OnDisable()`/`OnDestroy()` — never leave orphaned listeners. This matters more than usual here because floors are generated and torn down repeatedly within a single session.

## Resource systems (HP, PP, Paranoia/Nostalgia, money, bank)

- HP, per-ability PP, and Paranoia are three independent resources per the GDD — do not let one item or mechanic silently restore more than one of them unless the GDD explicitly says so (nostalgia fragments reduce Paranoia only; they do not heal HP or restore PP).
- The bank/loss-on-failure logic (lose half of unbanked money and items on nightmare failure, Paranoia resets to half) belongs in a dedicated system that runs once, at the "nightmare failed" transition — not scattered across individual item/currency scripts.
- Stat points earned per nightmare (HP, PP max, damage, defense) are spent in the hub, between nightmares, and are permanent — there is currently no reset/respec system in the GDD. Don't add one without flagging it as a design addition.

## Scene structure

- Expect at least two scene contexts: the **hub** (shop, bank, stat allocation, dialogue with "the Voice") and the **nightmare** (procedurally generated floor + turn-based gameplay). Keep hub-only and nightmare-only logic in separate assembly-friendly folders (see Folder structure) so hub UI code never ends up with a hard dependency on dungeon generation code or vice versa.

## Code conventions

- Use `[SerializeField] private` to expose fields to the Inspector instead of public fields.
- Never call `GetComponent`, `GetComponentInChildren`, or `Find` inside `Update()` — cache references in `Awake()`/`Start()`, or better, assign them via `[SerializeField]` from the editor.
- Avoid LINQ in per-frame gameplay code and inside the dungeon generator's hot paths (allocates garbage, adds iterator overhead) — the generator can run non-trivial loops per floor, so this matters more here than in typical UI code.
- Cache `Camera.main` in `Awake()` — never call it repeatedly in `Update()`.
- Prefer `async/await` (with UniTask if added to the project) over coroutines for new code, unless direct integration with `MonoBehaviour.StartCoroutine` is needed.
- If a null-check failure represents a real bug (not an expected state), use `Debug.LogError` instead of failing silently.
- Never use `#pragma warning disable` to silence warnings — fix the root cause instead.
- Naming: `PascalCase` for classes, methods, and public properties; `camelCase` for privates (`_health` or `health` — stay consistent with the rest of the codebase).

## Working with MCP for Unity

- Use MCP to create or modify GameObjects, prefabs, and components — **never edit scene/prefab YAML directly**.
- If a task requires a high volume of structural changes (many GameObjects, complex hierarchies — e.g. bulk-generating placeholder floor prefabs), a temporary editor script is allowed, but must:
  1. Live in `Assets/Editor/TemporaryGeneratedScripts/`
  2. Be deleted as the last step of the task
- Don't leave tasks for the user to finish manually in the Editor if they can be completed via MCP — finish all reachable work directly.
- Don't verify structural scene changes in Play Mode — confirm with the Editor stopped, since Play Mode changes don't persist.
- Before modifying a C# script, read it in full first — don't assume its contents from the filename or external references.

## Folder structure

```
Assets/
├── Scripts/
│   ├── Runtime/
│   │   ├── Data/            # ScriptableObject data containers (CharacterStatsSO, AbilitySO, ItemSO...)
│   │   ├── Events/          # GameEventSO and listeners
│   │   ├── TurnSystem/      # ITurnActor, turn resolver/queue
│   │   ├── DungeonGen/      # procedural generation: FloorData, DungeonGenerator, instantiation
│   │   ├── Combat/          # attack resolution, damage calc, PP consumption
│   │   ├── Resources/       # HP/Paranoia/PP runtime wiring, bank & loss-on-failure logic
│   │   ├── Hub/             # shop, stat allocation, dialogue/Voice, hub-only logic
│   │   └── UI/
│   └── Editor/
│       └── TemporaryGeneratedScripts/   # temporary editor scripts (do not commit)
├── Prefabs/
├── ScriptableObjects/       # SO asset instances (not scripts)
├── Scenes/                  # Hub.unity, Nightmare.unity (or per-nightmare scenes, TBD)
└── Art/
```

## Testing

- The dungeon generator is the highest-value target for Edit Mode Tests: given a fixed seed and depth params, assert on room count, connectivity (no unreachable rooms), and bounded enemy/item counts. It's decoupled from the Unity Editor lifecycle by design (see generation vs. instantiation split above), so it should be tested that way.
- Turn resolution order and resource math (damage, PP consumption, Paranoia increment/decay) are also good Edit Mode Test candidates — pure logic, no MonoBehaviour lifecycle involved.
- Systems that interact with MonoBehaviour's lifecycle (Update, collisions, tile-entry pickups) are verified with Play Mode Tests or manual testing guided through MCP.

## Wrapping up a task

- Confirm explicitly when work is complete (e.g. end with "Done" or a brief summary), especially on long tasks where it isn't obvious whether Claude is still working.
- If new assets were created via MCP (SOs, prefabs, scenes), name them explicitly along with their path.
- If a task touches something the GDD marks `[PENDIENTE]` or `[PROVISIONAL]`, say so explicitly in the summary — don't let an implementation detail quietly become the de facto final design.