using Jagara.Runtime.Data;
using Jagara.Runtime.Narrative;
using UnityEngine;

namespace Jagara.Runtime.UI.MenuActions
{
    /// <summary>
    /// Confirming this option removes the item the Item Action Panel was
    /// opened for from the inventory outright - no floor instance is spawned
    /// and no effect is applied, unlike Use or Drop.
    /// </summary>
    public class DestroyItemMenuAction : MonoBehaviour, IMenuAction
    {
        [SerializeField] private InventorySO inventory;
        [SerializeField] private ItemActionPanelController itemActionPanel;
        [SerializeField] private string description;
        [SerializeField] private MessageLogSO messageLog;
        [SerializeField] private MessageTemplateSO itemDestroyedMessage;

        public string Description => description;

        public void Execute()
        {
            // Read the item before removing it - the slot is null afterwards, and
            // the message needs its display name.
            int slotIndex = itemActionPanel.SelectedSlotIndex;
            ItemSO item = slotIndex >= 0 && slotIndex < inventory.Slots.Count ? inventory.Slots[slotIndex] : null;

            if (inventory.RemoveAt(slotIndex) && messageLog != null && item != null)
            {
                messageLog.Post(itemDestroyedMessage, StyledName.Item(item.DisplayName));
            }

            itemActionPanel.Close();
        }
    }
}
