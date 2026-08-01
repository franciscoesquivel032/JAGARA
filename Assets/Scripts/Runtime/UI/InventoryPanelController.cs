using System.Collections.Generic;
using Jagara.Runtime.Data;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Jagara.Runtime.UI
{
    /// <summary>
    /// Drives the Bag panel: shows only the player's occupied InventorySO slots
    /// (icon + name; empty slots are hidden entirely, not shown blank), moves a
    /// cursor between the visible slots using the same Menu action map
    /// ActionMenuController uses, auto-scrolls the selected slot into view,
    /// opens the Item Action Panel on Confirm, and closes itself back to the
    /// action menu via the Cancel action. Lives on the panel's own GameObject
    /// (ActionMenuController's leftMenuRoot), so it only runs Update()/
    /// OnEnable() while the panel is actually active - no separate "is panel
    /// open" flag needed.
    /// </summary>
    public class InventoryPanelController : MonoBehaviour
    {
        [System.Serializable]
        public struct SlotRow
        {
            public RectTransform rect;
            public Image icon;
            public TMP_Text label;
        }

        private const string EmptyInventoryDescription = "Your bag is empty.";

        [SerializeField] private InventorySO inventory;
        [SerializeField] private InputActionAsset controlsAsset;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform cursor;
        [SerializeField] private TextBoxController textBox;
        [SerializeField] private SlotRow[] slotRows;
        [SerializeField] private ItemActionPanelController itemActionPanel;

        [Tooltip("How many rows the Scroll View's viewport is sized to frame at once. " +
                 "The viewport's height must equal visibleRowCount*rowHeight + " +
                 "(visibleRowCount-1)*spacing + top/bottom padding of the Slots layout group, " +
                 "otherwise a row ends up half-clipped at one edge.")]
        [SerializeField] private int visibleRowCount = 4;

        private InputAction navigateUpAction;
        private InputAction navigateDownAction;
        private InputAction confirmAction;
        private InputAction cancelAction;

        // Indices into inventory.Slots/slotRows of the currently occupied (thus
        // visible) slots, in order. Navigation moves through this list, not
        // through raw slot indices, so empty slots are never selectable.
        private readonly List<int> occupiedSlotIndices = new();
        private int selectedPosition;

        // Position (not raw slot index) of the row currently framed at the top of
        // the viewport. The scroll offset is always exactly this many row pitches,
        // which is what keeps whole rows - never slivers - inside the viewport.
        private int firstVisiblePosition;

        // Mirrors ActionMenuController's own subPanelWasOpen flag: while the
        // Item Action Panel is active, Navigate/Confirm/Cancel are yielded to
        // it entirely; once it closes itself, the shared description text is
        // refreshed back to this panel's selected row.
        private bool subPanelWasOpen;

        private void Awake()
        {
            var map = controlsAsset.FindActionMap("Menu", throwIfNotFound: true);
            navigateUpAction = map.FindAction("NavigateUp", throwIfNotFound: true);
            navigateDownAction = map.FindAction("NavigateDown", throwIfNotFound: true);
            confirmAction = map.FindAction("Confirm", throwIfNotFound: true);
            cancelAction = map.FindAction("Cancel", throwIfNotFound: true);

            if (inventory == null)
            {
                Debug.LogError($"InventoryPanelController on {name}: inventory reference is not assigned.");
            }
        }

        private void OnEnable()
        {
            // NavigateUp/NavigateDown/Cancel are NOT enabled/disabled here: FindActionMap
            // returns the same underlying InputAction instances ActionMenuController
            // already reads from the same "Menu" map. ActionMenuController owns their
            // enabled lifetime for as long as the whole menu system is active; disabling
            // them here on close would (and did) also disable them for the action menu.
            if (inventory != null)
            {
                inventory.OnInventoryChanged += Refresh;
            }

            selectedPosition = 0;
            firstVisiblePosition = 0;
            Refresh();
        }

        private void OnDisable()
        {
            if (inventory != null)
            {
                inventory.OnInventoryChanged -= Refresh;
            }

            // The Item Action Panel is a Canvas sibling, not a child of this
            // GameObject - closing the Bag by any path (its own Cancel, or
            // ActionMenuController.Close() via Escape/Cancel/ToggleMenu) would
            // otherwise leave it active and visible with no parent panel to
            // return to.
            if (itemActionPanel != null)
            {
                itemActionPanel.Close();
            }
        }

        private void Update()
        {
            bool subPanelOpen = itemActionPanel != null && itemActionPanel.gameObject.activeSelf;
            if (subPanelOpen)
            {
                // Yield Navigate/Confirm/Cancel to the Item Action Panel's own
                // controller while it has focus - it reads the same Menu
                // action map independently.
                subPanelWasOpen = true;
                return;
            }

            if (subPanelWasOpen)
            {
                // The sub-panel just closed itself - refresh the shared
                // description text back to this panel's selected row instead
                // of leaving the sub-panel's last text showing.
                subPanelWasOpen = false;
                UpdateDescription();
            }

            if (navigateUpAction.WasPressedThisFrame())
            {
                Move(-1);
            }
            else if (navigateDownAction.WasPressedThisFrame())
            {
                Move(1);
            }

            if (cancelAction.WasPressedThisFrame())
            {
                gameObject.SetActive(false);
            }

            if (confirmAction.WasPressedThisFrame() && occupiedSlotIndices.Count > 0 && itemActionPanel != null)
            {
                itemActionPanel.Open(occupiedSlotIndices[selectedPosition]);
            }
        }

        private void Move(int delta)
        {
            if (occupiedSlotIndices.Count == 0)
            {
                return;
            }

            int next = Mathf.Clamp(selectedPosition + delta, 0, occupiedSlotIndices.Count - 1);
            if (next == selectedPosition)
            {
                return;
            }

            selectedPosition = next;
            UpdateCursorPosition();
            UpdateDescription();
            UpdateScroll();
        }

        private void Refresh()
        {
            if (slotRows == null || inventory == null)
            {
                return;
            }

            occupiedSlotIndices.Clear();
            for (int i = 0; i < slotRows.Length; i++)
            {
                ItemSO item = i < inventory.Slots.Count ? inventory.Slots[i] : null;
                bool occupied = item != null;

                // Empty slots are hidden entirely (not shown blank) - an inactive
                // row is skipped by the VerticalLayoutGroup, so the list compacts
                // down to only the occupied slots.
                slotRows[i].rect.gameObject.SetActive(occupied);
                if (occupied)
                {
                    occupiedSlotIndices.Add(i);
                }
            }

            bool hasItems = occupiedSlotIndices.Count > 0;
            if (cursor != null)
            {
                cursor.gameObject.SetActive(hasItems);
            }

            if (!hasItems)
            {
                selectedPosition = 0;
                UpdateDescription();
                return;
            }

            selectedPosition = Mathf.Clamp(selectedPosition, 0, occupiedSlotIndices.Count - 1);

            // Force the row layout to settle before reading row/label widths below -
            // mirrors ActionMenuController.Open()'s ForceRebuildLayoutImmediate call.
            // The label's width comes from its LayoutElement.flexibleWidth (not its
            // own text content), so this single rebuild is valid both for sizing the
            // truncation below and for the cursor/scroll row positions read later.
            if (scrollRect != null && scrollRect.content != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);
            }

            foreach (int i in occupiedSlotIndices)
            {
                ApplySlotVisual(slotRows[i], inventory.Slots[i]);
            }

            UpdateCursorPosition();
            UpdateDescription();
            UpdateScroll();
        }

        private static void ApplySlotVisual(SlotRow row, ItemSO item)
        {
            row.icon.sprite = item.Sprite;
            row.icon.enabled = item.Sprite != null;
            row.label.text = TruncateToFit(row.label, item.DisplayName);
        }

        private const string TruncationSuffix = "...";

        /// <summary>
        /// Manually truncates and appends "..." (three ASCII periods) instead of
        /// using TMP's built-in Ellipsis overflow mode: the Pixelon SDF font's
        /// atlas is static and has no "…" (U+2026) glyph, so Ellipsis mode silently
        /// renders nothing for it. Measures against the label's actual laid-out
        /// width, which is stable/content-independent since it comes from
        /// LayoutElement.flexibleWidth, not TMP's own preferred size.
        /// </summary>
        private static string TruncateToFit(TMP_Text label, string fullText)
        {
            float maxWidth = label.rectTransform.rect.width;
            if (maxWidth <= 0f)
            {
                label.text = fullText;
                return fullText;
            }

            label.text = fullText;
            label.ForceMeshUpdate();
            if (label.preferredWidth <= maxWidth)
            {
                return fullText;
            }

            for (int length = fullText.Length - 1; length > 0; length--)
            {
                string candidate = fullText.Substring(0, length).TrimEnd() + TruncationSuffix;
                label.text = candidate;
                label.ForceMeshUpdate();
                if (label.preferredWidth <= maxWidth)
                {
                    return candidate;
                }
            }

            label.text = TruncationSuffix;
            return TruncationSuffix;
        }

        private void UpdateCursorPosition()
        {
            if (cursor == null || occupiedSlotIndices.Count == 0)
            {
                return;
            }

            RectTransform row = slotRows[occupiedSlotIndices[selectedPosition]].rect;
            cursor.anchoredPosition = new Vector2(cursor.anchoredPosition.x, row.anchoredPosition.y);
        }

        private void UpdateDescription()
        {
            if (textBox == null)
            {
                return;
            }

            if (inventory == null || occupiedSlotIndices.Count == 0)
            {
                textBox.SetDescription(EmptyInventoryDescription);
                return;
            }

            ItemSO selected = inventory.Slots[occupiedSlotIndices[selectedPosition]];
            textBox.SetDescription(selected.Description);
        }

        /// <summary>
        /// Scrolls in whole-row steps: the content is only ever offset by an exact
        /// multiple of the row pitch, so the viewport always frames visibleRowCount
        /// complete rows - stepping past the last visible one hides the row at the
        /// opposite edge entirely instead of leaving a clipped sliver of it.
        /// <para>
        /// Deliberately not the "scroll by the minimum distance that brings the
        /// selected row into view" approach this replaced: that lands on whatever
        /// offset happens to expose the row's far edge, which is only a whole number
        /// of rows when the viewport height, spacing and padding line up exactly -
        /// and when they don't, every step leaves a few pixels of the previous row
        /// showing over the panel border.
        /// </para>
        /// Driven entirely by positions, so it behaves identically for 5 rows and 25.
        /// </summary>
        private void UpdateScroll()
        {
            if (scrollRect == null || scrollRect.content == null || occupiedSlotIndices.Count == 0)
            {
                return;
            }

            // Pull the framed window just far enough to contain the selection, then
            // clamp it to the list so the last page can't scroll past the final row.
            firstVisiblePosition = Mathf.Clamp(firstVisiblePosition, selectedPosition - visibleRowCount + 1, selectedPosition);
            firstVisiblePosition = Mathf.Clamp(firstVisiblePosition, 0, Mathf.Max(0, occupiedSlotIndices.Count - visibleRowCount));

            RectTransform content = scrollRect.content;
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, firstVisiblePosition * GetRowPitch());
        }

        /// <summary>
        /// Vertical distance between the top edges of two consecutive rows (row
        /// height + the layout group's spacing), measured from the rows as actually
        /// laid out rather than duplicated here as serialized constants - so
        /// retuning the VerticalLayoutGroup's spacing or the rows' LayoutElement
        /// height in the Editor keeps the scroll steps correct automatically.
        /// Callers only reach this after Refresh() has forced a layout rebuild.
        /// </summary>
        private float GetRowPitch()
        {
            if (occupiedSlotIndices.Count < 2)
            {
                // A single row always fits, so the pitch is never applied anyway.
                return 0f;
            }

            float first = slotRows[occupiedSlotIndices[0]].rect.anchoredPosition.y;
            float second = slotRows[occupiedSlotIndices[1]].rect.anchoredPosition.y;
            return Mathf.Abs(second - first);
        }
    }
}
