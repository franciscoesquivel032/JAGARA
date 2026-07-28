# Menu Option Descriptions Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Each action-menu option (Atacar, Objetos, Salir, Esperar) exposes a description string that the Text Box shows when the cursor lands on that option.

**Architecture:** `IMenuAction` gains a `Description` member alongside `Execute()`. `ActionMenuController` already resolves one `IMenuAction` per row in `Awake()`, so it reads `Description` off the same reference and writes it into a `TMP_Text` field on cursor move / menu open — no new lookups, no new components.

**Tech Stack:** Unity 6000.5.3f1, C#, TextMeshPro (`TMPro.TMP_Text`), MCP for Unity (for all scene/Inspector wiring — per `CLAUDE.md`, scene YAML is never hand-edited).

## Global Constraints

- Never edit `.unity` scene YAML directly — all scene/Inspector wiring goes through MCP for Unity tools.
- `[SerializeField] private` for Inspector-exposed fields (existing convention in this codebase).
- Missing/null `IMenuAction` on a row → Text Box shows empty string, not stale text.
- Description copy (Spanish, from the approved spec `docs/superpowers/specs/2026-07-28-menu-option-descriptions-design.md`):
  - Atacar: `Selecciona un ataque para usar en tu turno.`
  - Objetos: `Abre tu inventario para usar un objeto.`
  - Salir: `Cierra el menú de acciones.`
  - Esperar: `Pasa tu turno sin realizar ninguna acción.`

---

### Task 1: `IMenuAction` gains `Description`, all implementers updated

**Files:**
- Modify: `Assets/Scripts/Runtime/UI/MenuActions/IMenuAction.cs`
- Modify: `Assets/Scripts/Runtime/UI/MenuActions/OpenPanelMenuAction.cs`
- Modify: `Assets/Scripts/Runtime/UI/MenuActions/CloseMenuAction.cs`
- Modify: `Assets/Scripts/Runtime/UI/MenuActions/EndTurnMenuAction.cs`

**Interfaces:**
- Produces: `IMenuAction.Description` (`string`, read-only) — consumed by `ActionMenuController` in Task 2.

These four files change together: the interface can't compile until every implementer satisfies it. There's no automated test here (this is pure Inspector-facing scaffolding, not branching logic) — correctness is verified by a clean Unity compile.

- [ ] **Step 1: Add `Description` to the interface**

`Assets/Scripts/Runtime/UI/MenuActions/IMenuAction.cs`:

```csharp
namespace Jagara.Runtime.UI.MenuActions
{
    /// <summary>
    /// A confirmable behavior attached to a menu option row. ActionMenuController
    /// looks this up per row and calls Execute() on confirm - it never needs to
    /// know which concrete behavior a given row has.
    /// </summary>
    public interface IMenuAction
    {
        string Description { get; }
        void Execute();
    }
}
```

- [ ] **Step 2: Implement `Description` on `OpenPanelMenuAction`**

`Assets/Scripts/Runtime/UI/MenuActions/OpenPanelMenuAction.cs`:

```csharp
using UnityEngine;

namespace Jagara.Runtime.UI.MenuActions
{
    /// <summary>Confirming this option shows another panel (e.g. a sub-menu).</summary>
    public class OpenPanelMenuAction : MonoBehaviour, IMenuAction
    {
        [SerializeField] private GameObject targetPanel;
        [SerializeField] private string description;

        public string Description => description;

        public void Execute()
        {
            targetPanel.SetActive(true);
        }
    }
}
```

- [ ] **Step 3: Implement `Description` on `CloseMenuAction`**

`Assets/Scripts/Runtime/UI/MenuActions/CloseMenuAction.cs`:

```csharp
using UnityEngine;

namespace Jagara.Runtime.UI.MenuActions
{
    /// <summary>Confirming this option closes the action menu.</summary>
    public class CloseMenuAction : MonoBehaviour, IMenuAction
    {
        [SerializeField] private ActionMenuController actionMenu;
        [SerializeField] private string description;

        public string Description => description;

        public void Execute()
        {
            actionMenu.Close();
        }
    }
}
```

- [ ] **Step 4: Implement `Description` on `EndTurnMenuAction`**

`Assets/Scripts/Runtime/UI/MenuActions/EndTurnMenuAction.cs`:

```csharp
using UnityEngine;

namespace Jagara.Runtime.UI.MenuActions
{
    /// <summary>
    /// Confirming this option passes the player's turn without moving. Closes
    /// the menu first so enemy turns (and any resulting animations) resolve
    /// with the menu already off screen, not behind it.
    /// </summary>
    public class EndTurnMenuAction : MonoBehaviour, IMenuAction
    {
        [SerializeField] private ActionMenuController actionMenu;
        [SerializeField] private string description;

        public string Description => description;

        public void Execute()
        {
            actionMenu.Close();
            actionMenu.EndPlayerTurn();
        }
    }
}
```

- [ ] **Step 5: Verify compile**

Use MCP `refresh_unity`, then `read_console` (errors only). Expected: no compile errors mentioning `IMenuAction`, `OpenPanelMenuAction`, `CloseMenuAction`, or `EndTurnMenuAction`.

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/Runtime/UI/MenuActions/IMenuAction.cs Assets/Scripts/Runtime/UI/MenuActions/OpenPanelMenuAction.cs Assets/Scripts/Runtime/UI/MenuActions/CloseMenuAction.cs Assets/Scripts/Runtime/UI/MenuActions/EndTurnMenuAction.cs
git commit -m "feat: add Description to IMenuAction and its implementers"
```

---

### Task 2: `ActionMenuController` writes the selected option's description to the Text Box

**Files:**
- Modify: `Assets/Scripts/Runtime/UI/ActionMenuController.cs`

**Interfaces:**
- Consumes: `IMenuAction.Description` (`string`, from Task 1).
- Produces: `ActionMenuController.descriptionText` (`[SerializeField] private TMP_Text`) — the Inspector slot Task 3 wires up in the scene.

No automated test (MonoBehaviour lifecycle code, per `CLAUDE.md` testing guidance) — verified by compile now, by live Play Mode behavior in Task 3.

- [ ] **Step 1: Add the `TMPro` using directive and the `descriptionText` field**

In `Assets/Scripts/Runtime/UI/ActionMenuController.cs`, add to the using block (after `using UnityEngine.InputSystem;`):

```csharp
using TMPro;
```

Add alongside the existing `[SerializeField]` fields (after `cursor`):

```csharp
[SerializeField] private TMP_Text descriptionText;
```

- [ ] **Step 2: Add `UpdateDescription()` and call it from `Open()` and `Move()`**

Add this private method near `UpdateCursorPosition()`:

```csharp
private void UpdateDescription()
{
    descriptionText.text = optionActions[selectedIndex]?.Description ?? string.Empty;
}
```

Update `Open()` to call it after the cursor is positioned:

```csharp
public void Open()
{
    actionMenuRoot.SetActive(true);
    IsOpen = true;
    selectedIndex = 0;
    LayoutRebuilder.ForceRebuildLayoutImmediate(optionsContainer);
    UpdateCursorPosition();
    UpdateDescription();
}
```

Update `Move()` to call it after `selectedIndex` changes:

```csharp
private void Move(int delta)
{
    int next = Mathf.Clamp(selectedIndex + delta, 0, optionRects.Length - 1);
    if (next == selectedIndex)
    {
        return;
    }

    selectedIndex = next;
    UpdateCursorPosition();
    UpdateDescription();
}
```

- [ ] **Step 3: Verify compile**

Use MCP `refresh_unity`, then `read_console` (errors only). Expected: no compile errors mentioning `ActionMenuController`.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Runtime/UI/ActionMenuController.cs
git commit -m "feat: sync Text Box to the selected menu option's description"
```

---

### Task 3: Wire the scene via MCP and verify in Play Mode

**Files:**
- Scene (MCP only, no direct YAML edit): `Assets/Scenes/DungeonPreview.unity`

**Interfaces:**
- Consumes: `ActionMenuController.descriptionText` field (Task 2), `IMenuAction.description` field on each row's action component (Task 1).

This is the task that actually makes the feature visible — everything before this is scaffolding.

- [ ] **Step 1: Wire `descriptionText`**

Via MCP `manage_gameobject`/`manage_components` (or `execute_code` if more direct): on the `ActionMenuController` GameObject in `DungeonPreview.unity`, set the `descriptionText` field to the `Text (TMP)` object living under `Text Box > Frame > Text (TMP)`.

- [ ] **Step 2: Set each row's `description` value**

On each option row's action component, set `description` to the copy from Global Constraints above:
- `Option_Atacar`'s `OpenPanelMenuAction.description` → `Selecciona un ataque para usar en tu turno.`
- `Option_Objetos`'s `OpenPanelMenuAction.description` → `Abre tu inventario para usar un objeto.`
- `Option_Salir`'s `CloseMenuAction.description` → `Cierra el menú de acciones.`
- `Option_Esperar`'s `EndTurnMenuAction.description` → `Pasa tu turno sin realizar ninguna acción.`

- [ ] **Step 3: Manual Play Mode verification**

Enter Play Mode (via MCP `manage_editor`). Open the action menu. Confirm:
- On open, the Text Box shows the Atacar description (index 0 is selected by default).
- Moving the cursor to Objetos, Salir, and Esperar updates the Text Box to each one's description.
- No console errors during navigation.

Exit Play Mode when done (structural scene state doesn't persist from Play Mode, but this task made no structural changes in Play Mode — only read behavior).

- [ ] **Step 4: Commit**

The scene file will show as modified (`Assets/Scenes/DungeonPreview.unity`) after MCP wiring.

```bash
git add Assets/Scenes/DungeonPreview.unity
git commit -m "feat: wire Text Box and option descriptions in DungeonPreview scene"
```
