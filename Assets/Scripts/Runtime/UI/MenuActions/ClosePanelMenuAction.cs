using UnityEngine;

namespace Jagara.Runtime.UI.MenuActions
{
    /// <summary>Confirming this option hides another panel (e.g. a sub-menu). Generic counterpart to OpenPanelMenuAction.</summary>
    public class ClosePanelMenuAction : MonoBehaviour, IMenuAction
    {
        [SerializeField] private GameObject targetPanel;
        [SerializeField] private string description;

        public string Description => description;

        public void Execute()
        {
            targetPanel.SetActive(false);
        }
    }
}
