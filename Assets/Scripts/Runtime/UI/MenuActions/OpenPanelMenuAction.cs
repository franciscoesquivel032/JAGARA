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
