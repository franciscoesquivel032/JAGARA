using Jagara.Runtime.Data;
using UnityEngine;

namespace Jagara.Runtime.Gameplay
{
    /// <summary>
    /// A single dropped item sitting on a dungeon floor tile: holds the ItemSO
    /// it represents and displays its sprite. Pickup-on-entry logic is out of
    /// scope for now - this only carries the data and renders it.
    /// </summary>
    public class ItemMarker : MonoBehaviour
    {
        private SpriteRenderer spriteRenderer;

        public ItemSO Item { get; private set; }

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                Debug.LogError($"ItemMarker on {name}: no SpriteRenderer found; item will render without a sprite.");
            }
        }

        public void Initialize(ItemSO item)
        {
            Item = item;
            if (item == null)
            {
                Debug.LogError($"ItemMarker on {name}: Initialize called with a null ItemSO.");
                return;
            }

            if (spriteRenderer != null)
            {
                spriteRenderer.sprite = item.Sprite;
            }
        }
    }
}
