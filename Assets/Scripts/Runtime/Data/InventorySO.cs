using System;
using System.Collections.Generic;
using UnityEngine;

namespace Jagara.Runtime.Data
{
    /// <summary>
    /// Runtime inventory state: a fixed number of slots, each holding at most
    /// one ItemSO (no stacking). Lives as an SO rather than a MonoBehaviour so
    /// it survives Hub &lt;-&gt; Nightmare scene transitions without extra wiring,
    /// the same way FloatVariableSO-style runtime values do. OnInventoryChanged
    /// is a plain C# event for now - promote to a GameEventSO if/when a
    /// cross-system UI listener actually needs one.
    /// </summary>
    [CreateAssetMenu(fileName = "New Inventory", menuName = "Jagara/Inventory")]
    public class InventorySO : ScriptableObject
    {
        [Tooltip("GDD: fixed at 5. Kept as a field for inspector visibility and testability.")]
        [SerializeField] private int maxSlots = 5;

        private ItemSO[] slots;

        public int MaxSlots => maxSlots;
        public IReadOnlyList<ItemSO> Slots => slots;

        public event Action OnInventoryChanged = delegate { };

        private void OnEnable()
        {
            slots = new ItemSO[maxSlots];
        }

        public bool IsFull()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null)
                {
                    return false;
                }
            }
            return true;
        }

        public bool TryAddItem(ItemSO item)
        {
            if (item == null)
            {
                Debug.LogError("InventorySO.TryAddItem: item is null.");
                return false;
            }

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null)
                {
                    slots[i] = item;
                    OnInventoryChanged();
                    return true;
                }
            }

            return false;
        }

        public bool RemoveAt(int index)
        {
            if (index < 0 || index >= slots.Length || slots[index] == null)
            {
                return false;
            }

            slots[index] = null;
            OnInventoryChanged();
            return true;
        }

        public bool TryUseItem(int index, GameObject user)
        {
            if (index < 0 || index >= slots.Length || slots[index] == null)
            {
                return false;
            }

            ItemSO item = slots[index];
            item.Effect?.Apply(user);

            if (item.ConsumedOnUse)
            {
                slots[index] = null;
            }

            OnInventoryChanged();
            return true;
        }
    }
}
