using Jagara.Runtime.Data;
using UnityEngine;

namespace Jagara.Runtime.UI.MenuActions
{
    /// <summary>
    /// Confirming this option uses the item the Item Action Panel was opened
    /// for, then returns to the Bag and ends the player's turn - using an item
    /// is one of the GDD's three turn actions (move/attack/use item), unlike
    /// Drop/Destroy which are free inventory management.
    /// </summary>
    public class UseItemMenuAction : MonoBehaviour, IMenuAction
    {
        [SerializeField] private InventorySO inventory;
        [SerializeField] private ItemActionPanelController itemActionPanel;
        [SerializeField] private ActionMenuController actionMenu;
        [SerializeField] private string description;

        public string Description => description;

        public void Execute()
        {
            var player = itemActionPanel.Player;
            inventory.TryUseItem(itemActionPanel.SelectedSlotIndex, player != null ? player.gameObject : null);
            itemActionPanel.Close();
            actionMenu.EndPlayerTurn();
        }
    }
}
