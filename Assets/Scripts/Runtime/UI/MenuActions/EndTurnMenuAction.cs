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
