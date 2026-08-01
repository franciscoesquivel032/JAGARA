using System.Collections.Generic;
using Jagara.Runtime.Gameplay;
using Jagara.Runtime.UI.MenuActions;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Jagara.Runtime.UI
{
    /// <summary>
    /// Drives the Use/Drop/Destroy/Close panel opened by confirming a slot in
    /// the Bag panel. Same shape as ActionMenuController (cursor over option
    /// rows, Strategy-pattern row confirmation via IMenuAction), but one level
    /// deeper: InventoryPanelController yields Navigate/Confirm to this panel
    /// while it's active, the same way ActionMenuController yields to
    /// InventoryPanelController. NavigateUp/NavigateDown/Confirm/Cancel are
    /// NOT enabled/disabled here - this panel can only ever be open while the
    /// Bag is open, so those shared InputAction instances are already enabled
    /// by ActionMenuController/InventoryPanelController for as long as this
    /// panel could possibly need them. Disabling them on Close() here would
    /// also disable them for the still-open Bag panel underneath.
    /// </summary>
    public class ItemActionPanelController : MonoBehaviour
    {
        [SerializeField] private InputActionAsset controlsAsset;
        [SerializeField] private RectTransform optionsContainer;
        [SerializeField] private RectTransform cursor;
        [SerializeField] private TextBoxController textBox;

        private InputAction navigateUpAction;
        private InputAction navigateDownAction;
        private InputAction confirmAction;
        private InputAction cancelAction;

        private RectTransform[] optionRects;
        private IMenuAction[] optionActions;
        private int selectedIndex;

        public int SelectedSlotIndex { get; private set; }
        public PlayerController Player { get; private set; }

        private void Awake()
        {
            var map = controlsAsset.FindActionMap("Menu", throwIfNotFound: true);
            navigateUpAction = map.FindAction("NavigateUp", throwIfNotFound: true);
            navigateDownAction = map.FindAction("NavigateDown", throwIfNotFound: true);
            confirmAction = map.FindAction("Confirm", throwIfNotFound: true);
            cancelAction = map.FindAction("Cancel", throwIfNotFound: true);

            var rows = new List<RectTransform>(optionsContainer.childCount);
            var actions = new List<IMenuAction>(optionsContainer.childCount);
            for (int i = 0; i < optionsContainer.childCount; i++)
            {
                var child = (RectTransform)optionsContainer.GetChild(i);

                var layoutElement = child.GetComponent<LayoutElement>();
                if (layoutElement != null && layoutElement.ignoreLayout)
                {
                    continue;
                }

                rows.Add(child);

                var action = child.GetComponent<IMenuAction>();
                if (action == null)
                {
                    Debug.LogWarning($"ItemActionPanelController: option row '{child.name}' has no IMenuAction component - confirming it will do nothing.");
                }
                actions.Add(action);
            }
            optionRects = rows.ToArray();
            optionActions = actions.ToArray();
        }

        /// <summary>
        /// Called once by NightmareBootstrap after the player is spawned - the
        /// player is instantiated at runtime, so it can't be wired via the
        /// Inspector the way the other scene-resident references are.
        /// </summary>
        public void Initialize(PlayerController player)
        {
            Player = player;
        }

        public void Open(int slotIndex)
        {
            SelectedSlotIndex = slotIndex;
            gameObject.SetActive(true);
            selectedIndex = 0;
            LayoutRebuilder.ForceRebuildLayoutImmediate(optionsContainer);
            UpdateCursorPosition();
            UpdateDescription();
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Overwrites the shared description text with a one-off message (e.g.
        /// a blocked-drop notice) without closing the panel. The next cursor
        /// move naturally overwrites it back to the selected row's description.
        /// </summary>
        public void ShowMessage(string message)
        {
            textBox?.SetDescription(message);
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

            if (confirmAction.WasPressedThisFrame())
            {
                optionActions[selectedIndex]?.Execute();
            }

            if (cancelAction.WasPressedThisFrame())
            {
                Close();
            }
        }

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

        private void UpdateCursorPosition()
        {
            cursor.anchoredPosition = new Vector2(cursor.anchoredPosition.x, optionRects[selectedIndex].anchoredPosition.y);
        }

        private void UpdateDescription()
        {
            textBox?.SetDescription(optionActions[selectedIndex]?.Description ?? string.Empty);
        }
    }
}
