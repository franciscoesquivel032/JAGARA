# Menu option descriptions in the Text Box

## Problem

The action menu (`ActionMenuController`) shows four options (Atacar, Objetos, Salir, Esperar) but gives no in-context explanation of what each does. The scene already has a `Text Box` GameObject (child `Text (TMP)`, a `TextMeshProUGUI`) currently holding placeholder flavor text. When the cursor is on an option, that box should show a description of what confirming it will do.

## Design

1. **`IMenuAction`** (`Assets/Scripts/Runtime/UI/MenuActions/IMenuAction.cs`) gains a read-only `string Description { get; }` member alongside `Execute()`. `ActionMenuController` already resolves one `IMenuAction` per row in `Awake()`, so no additional lookup is needed to read a description.
2. Each concrete action — `OpenPanelMenuAction`, `CloseMenuAction`, `EndTurnMenuAction` — gets `[SerializeField] private string description;` and `public string Description => description;`. The value is set per-instance in the Inspector, so e.g. `Option_Atacar`'s `OpenPanelMenuAction` and `Option_Objetos`'s `OpenPanelMenuAction` (same class) hold independent text.
3. **`ActionMenuController`** gets `[SerializeField] private TMP_Text descriptionText;`, wired to the existing `Text (TMP)` object under `Text Box`. A private `UpdateDescription()` sets `descriptionText.text = optionActions[selectedIndex]?.Description ?? string.Empty;`. Called from both `Open()` (so the description is correct the instant the menu appears, defaulting to index 0) and `Move()` (after `selectedIndex` changes).
4. If a row's `IMenuAction` is null (the existing "confirming will do nothing" warning case in `Awake()`), the Text Box shows an empty string rather than stale text.
5. The scene's current placeholder text ("Tengo la camisa negra...") is replaced by the live description once wiring is in place.

## Content (Spanish, matching existing option labels)

- **Atacar**: "Selecciona un ataque para usar en tu turno."
- **Objetos**: "Abre tu inventario para usar un objeto."
- **Salir**: "Cierra el menú de acciones."
- **Esperar**: "Pasa tu turno sin realizar ninguna acción."

## Implementation notes

- Scene wiring (the new `descriptionText` reference, and each row's `description` value) is done via MCP for Unity tools, not direct `.unity` YAML edits, per project convention.
- No new files beyond edits to the three existing `IMenuAction` implementations, the interface, and `ActionMenuController`.

## Out of scope

- No changes to menu navigation, layout, or the `IMenuAction` `Execute()` contract.
- No localization system — description strings are plain Inspector-set strings like the existing option labels.
