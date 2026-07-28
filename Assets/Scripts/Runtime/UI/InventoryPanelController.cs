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
    /// ActionMenuController uses, auto-scrolls the selected slot into view, and
    /// closes itself back to the action menu via the Cancel action. Lives on the
    /// panel's own GameObject (ActionMenuController's leftMenuRoot), so it only
    /// runs Update()/OnEnable() while the panel is actually active - no separate
    /// "is panel open" flag needed.
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
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private SlotRow[] slotRows;

        private InputAction navigateUpAction;
        private InputAction navigateDownAction;
        private InputAction cancelAction;

        // Indices into inventory.Slots/slotRows of the currently occupied (thus
        // visible) slots, in order. Navigation moves through this list, not
        // through raw slot indices, so empty slots are never selectable.
        private readonly List<int> occupiedSlotIndices = new();
        private int selectedPosition;

        private void Awake()
        {
            var map = controlsAsset.FindActionMap("Menu", throwIfNotFound: true);
            navigateUpAction = map.FindAction("NavigateUp", throwIfNotFound: true);
            navigateDownAction = map.FindAction("NavigateDown", throwIfNotFound: true);
            cancelAction = map.FindAction("Cancel", throwIfNotFound: true);

            if (inventory == null)
            {
                Debug.LogError($"InventoryPanelController on {name}: inventory reference is not assigned.");
            }
        }

        private void OnEnable()
        {
            // NavigateUp/NavigateDown are NOT enabled/disabled here: FindActionMap
            // returns the same underlying InputAction instances ActionMenuController
            // already reads from the same "Menu" map. ActionMenuController owns their
            // enabled lifetime for as long as the whole menu system is active; disabling
            // them here on close would (and did) also disable them for the action menu.
            cancelAction.Enable();

            if (inventory != null)
            {
                inventory.OnInventoryChanged += Refresh;
            }

            selectedPosition = 0;
            Refresh();
        }

        private void OnDisable()
        {
            cancelAction.Disable();

            if (inventory != null)
            {
                inventory.OnInventoryChanged -= Refresh;
            }
        }

        private void Update()
        {
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
            if (descriptionText == null)
            {
                return;
            }

            if (inventory == null || occupiedSlotIndices.Count == 0)
            {
                descriptionText.text = EmptyInventoryDescription;
                return;
            }

            ItemSO selected = inventory.Slots[occupiedSlotIndices[selectedPosition]];
            descriptionText.text = selected.Description;
        }

        private static readonly Vector3[] CornersBuffer = new Vector3[4];

        /// <summary>
        /// Scrolls just enough to bring the selected row fully into the viewport -
        /// zero movement while the selection is already visible, unlike a naive
        /// index-based normalized-position remap (which shifts the whole list on
        /// every step and reads as "the scroll moves" rather than "the cursor moves").
        /// </summary>
        private void UpdateScroll()
        {
            if (scrollRect == null || scrollRect.viewport == null || scrollRect.content == null || occupiedSlotIndices.Count == 0)
            {
                return;
            }

            Canvas.ForceUpdateCanvases();

            RectTransform viewport = scrollRect.viewport;
            RectTransform content = scrollRect.content;
            RectTransform row = slotRows[occupiedSlotIndices[selectedPosition]].rect;

            row.GetWorldCorners(CornersBuffer);
            float rowTop = CornersBuffer[1].y;
            float rowBottom = CornersBuffer[0].y;

            viewport.GetWorldCorners(CornersBuffer);
            float viewTop = CornersBuffer[1].y;
            float viewBottom = CornersBuffer[0].y;

            if (rowTop > viewTop)
            {
                content.anchoredPosition -= new Vector2(0f, rowTop - viewTop);
            }
            else if (rowBottom < viewBottom)
            {
                content.anchoredPosition += new Vector2(0f, viewBottom - rowBottom);
            }
        }
    }
}
