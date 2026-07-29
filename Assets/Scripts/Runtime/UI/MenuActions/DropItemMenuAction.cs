using UnityEngine;

namespace Jagara.Runtime.UI.MenuActions
{
    /// <summary>
    /// Confirming this option drops the item the Item Action Panel was opened
    /// for onto the player's current tile. If the tile already has an item on
    /// it, the drop is blocked and the panel stays open (per design) so the
    /// player can pick a different action instead of losing their place.
    /// </summary>
    public class DropItemMenuAction : MonoBehaviour, IMenuAction
    {
        [SerializeField] private ItemActionPanelController itemActionPanel;
        [SerializeField] private string description;
        [SerializeField] private string blockedMessage = "There's no room to drop that here.";

        public string Description => description;

        public void Execute()
        {
            var player = itemActionPanel.Player;
            if (player != null && player.TryDropItem(itemActionPanel.SelectedSlotIndex))
            {
                itemActionPanel.Close();
            }
            else
            {
                itemActionPanel.ShowMessage(blockedMessage);
            }
        }
    }
}
