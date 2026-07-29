using Jagara.Runtime.Data;
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

        public string Description => description;

        public void Execute()
        {
            inventory.RemoveAt(itemActionPanel.SelectedSlotIndex);
            itemActionPanel.Close();
        }
    }
}
