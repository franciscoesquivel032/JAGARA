using Jagara.Runtime.Data;
using Jagara.Runtime.Narrative;
using UnityEngine;

namespace Jagara.Runtime.UI.MenuActions
{
    /// <summary>
    /// Confirming this option uses the item the Item Action Panel was opened
    /// for, closes the whole menu, and ends the player's turn - using an item is
    /// one of the GDD's three turn actions (move/attack/use item), unlike
    /// Drop/Destroy which are free inventory management and leave the Bag open.
    /// <para>
    /// Closing all the way out (rather than just dropping back to the Bag) is
    /// what makes the "You used the ..." line visible: the menus own the text box
    /// while they are open, and releasing it discards whatever was on it. See the
    /// ordering note in Execute.
    /// </para>
    /// </summary>
    public class UseItemMenuAction : MonoBehaviour, IMenuAction
    {
        [SerializeField] private InventorySO inventory;
        [SerializeField] private ItemActionPanelController itemActionPanel;
        [SerializeField] private ActionMenuController actionMenu;
        [SerializeField] private string description;
        [SerializeField] private MessageLogSO messageLog;
        [SerializeField] private MessageTemplateSO itemUsedMessage;

        public string Description => description;

        public void Execute()
        {
            var player = itemActionPanel.Player;

            // Read the item before using it - a consumable clears its slot.
            int slotIndex = itemActionPanel.SelectedSlotIndex;
            ItemSO item = slotIndex >= 0 && slotIndex < inventory.Slots.Count ? inventory.Slots[slotIndex] : null;

            bool used = inventory.TryUseItem(slotIndex, player != null ? player.gameObject : null);

            itemActionPanel.Close();
            actionMenu.Close();

            // Posted AFTER the menu closes, not before: ActionMenuController.Close
            // hands the text box back via ClearDescription, which wipes whatever
            // lines are on it. Posting first would put the message straight into
            // the window that Close is about to empty, and the player would never
            // see it. Also lands before EndPlayerTurn, so the item's line reads
            // above whatever the enemies do in response.
            if (used && messageLog != null && item != null)
            {
                messageLog.Post(itemUsedMessage, StyledName.Item(item.DisplayName));
            }

            actionMenu.EndPlayerTurn();
        }
    }
}
