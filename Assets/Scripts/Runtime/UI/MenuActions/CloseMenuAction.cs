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
