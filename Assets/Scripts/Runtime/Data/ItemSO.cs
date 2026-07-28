using UnityEngine;

namespace Jagara.Runtime.Data
{
    /// <summary>
    /// Static per-item-type data: display info and the effect it applies when
    /// used. See ItemEffectSO for why effects are a strategy reference rather
    /// than inline fields.
    /// </summary>
    [CreateAssetMenu(fileName = "New Item", menuName = "Jagara/Item")]
    public class ItemSO : ScriptableObject
    {
        [SerializeField] private string displayName;
        [SerializeField] private Sprite sprite;
        [SerializeField, TextArea] private string description;
        [SerializeField] private ItemEffectSO effect;

        [Tooltip("If true, using this item removes it from its inventory slot.")]
        [SerializeField] private bool consumedOnUse = true;

        public string DisplayName => displayName;
        public Sprite Sprite => sprite;
        public string Description => description;
        public ItemEffectSO Effect => effect;
        public bool ConsumedOnUse => consumedOnUse;
    }
}
