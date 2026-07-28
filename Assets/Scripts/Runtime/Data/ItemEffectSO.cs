using UnityEngine;

namespace Jagara.Runtime.Data
{
    /// <summary>
    /// Strategy base for what using an item does. Concrete effects (heal HP,
    /// reduce Paranoia, restore PP) are added as separate SO assets once the
    /// Resources system (Runtime/Resources/) exists - this base intentionally
    /// has no subclasses yet.
    /// </summary>
    public abstract class ItemEffectSO : ScriptableObject
    {
        public abstract void Apply(GameObject user);
    }
}
