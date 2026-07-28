using System.Collections.Generic;
using Jagara.Runtime.TurnSystem;
using Jagara.Runtime.UI.MenuActions;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Jagara.Runtime.UI
{
    /// <summary>
    /// Opens/closes the action menu panel, lets the player move a selection
    /// cursor up/down between its option rows, and confirms the selected row by
    /// delegating to whatever IMenuAction component is attached to it (Strategy
    /// pattern - this class never needs to know what a given option actually does).
    /// Lives on an always-active sibling of the menu panel it controls, since a
    /// script that SetActive(false)s its own GameObject would stop receiving
    /// Update() and could never reopen it.
    /// </summary>
    public class ActionMenuController : MonoBehaviour
    {
        [SerializeField] private InputActionAsset controlsAsset;
        [SerializeField] private GameObject actionMenuRoot;
        [SerializeField] private GameObject leftMenuRoot;
        [SerializeField] private RectTransform optionsContainer;
        [SerializeField] private RectTransform cursor;
        [SerializeField] private TMP_Text descriptionText;

        private InputAction toggleAction;
        private InputAction closeAction;
        private InputAction navigateUpAction;
        private InputAction navigateDownAction;
        private InputAction confirmAction;

        private RectTransform[] optionRects;
        private IMenuAction[] optionActions;
        private int selectedIndex;
        private TurnResolver turnResolver;
        private bool subPanelWasOpen;

        public bool IsOpen { get; private set; }

        private void Awake()
        {
            var map = controlsAsset.FindActionMap("Menu", throwIfNotFound: true);
            toggleAction = map.FindAction("ToggleMenu", throwIfNotFound: true);
            closeAction = map.FindAction("CloseMenu", throwIfNotFound: true);
            navigateUpAction = map.FindAction("NavigateUp", throwIfNotFound: true);
            navigateDownAction = map.FindAction("NavigateDown", throwIfNotFound: true);
            confirmAction = map.FindAction("Confirm", throwIfNotFound: true);

            var rows = new List<RectTransform>(optionsContainer.childCount);
            var actions = new List<IMenuAction>(optionsContainer.childCount);
            for (int i = 0; i < optionsContainer.childCount; i++)
            {
                var child = (RectTransform)optionsContainer.GetChild(i);

                // The selection cursor is parented here too (so its anchoredPosition
                // shares the rows' coordinate space), marked via ignoreLayout - skip it,
                // it isn't a selectable option.
                var layoutElement = child.GetComponent<LayoutElement>();
                if (layoutElement != null && layoutElement.ignoreLayout)
                {
                    continue;
                }

                rows.Add(child);

                var action = child.GetComponent<IMenuAction>();
                if (action == null)
                {
                    Debug.LogWarning($"ActionMenuController: option row '{child.name}' has no IMenuAction component - confirming it will do nothing.");
                }
                actions.Add(action);
            }
            optionRects = rows.ToArray();
            optionActions = actions.ToArray();
        }

        private void OnEnable()
        {
            toggleAction.Enable();
            closeAction.Enable();
            navigateUpAction.Enable();
            navigateDownAction.Enable();
            confirmAction.Enable();
        }

        private void OnDisable()
        {
            toggleAction.Disable();
            closeAction.Disable();
            navigateUpAction.Disable();
            navigateDownAction.Disable();
            confirmAction.Disable();
        }

        private void Update()
        {
            if (toggleAction.WasPressedThisFrame())
            {
                Toggle();
            }

            if (IsOpen && closeAction.WasPressedThisFrame())
            {
                Close();
            }

            if (!IsOpen)
            {
                return;
            }

            bool subPanelOpen = leftMenuRoot != null && leftMenuRoot.activeSelf;
            if (subPanelOpen)
            {
                // Yield Navigate/Confirm to the sub-panel's own controller (e.g.
                // InventoryPanelController) while it has focus - it reads the
                // same Menu action map independently.
                subPanelWasOpen = true;
                return;
            }

            if (subPanelWasOpen)
            {
                // The sub-panel just closed itself (its own Cancel action) - refresh
                // the shared description text back to this menu's selected row
                // instead of leaving the sub-panel's last text showing.
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

            if (confirmAction.WasPressedThisFrame())
            {
                Confirm();
            }
        }

        public void Toggle()
        {
            if (IsOpen)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        public void Open()
        {
            actionMenuRoot.SetActive(true);
            IsOpen = true;
            selectedIndex = 0;
            LayoutRebuilder.ForceRebuildLayoutImmediate(optionsContainer);
            UpdateCursorPosition();
            UpdateDescription();
        }

        public void Close()
        {
            actionMenuRoot.SetActive(false);
            IsOpen = false;
            subPanelWasOpen = false;

            if (leftMenuRoot != null)
            {
                leftMenuRoot.SetActive(false);
            }
        }

        public void Initialize(TurnResolver resolver)
        {
            turnResolver = resolver;
        }

        public void EndPlayerTurn() => turnResolver?.EndPlayerTurn();

        public void Confirm()
        {
            optionActions[selectedIndex]?.Execute();
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
            descriptionText.text = optionActions[selectedIndex]?.Description ?? string.Empty;
        }
    }
}
