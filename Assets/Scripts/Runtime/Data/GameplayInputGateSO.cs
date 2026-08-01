using System;
using UnityEngine;

namespace Jagara.Runtime.Data
{
    /// <summary>
    /// Shared "is something modal on screen?" flag. UI that takes over input
    /// (the action menu, a dialogue) pushes a block while it is open; the player
    /// only has to ask <see cref="IsBlocked"/> instead of holding a reference to
    /// every such panel.
    /// <para>
    /// It is a ScriptableObject specifically so the player prefab can reference
    /// it via [SerializeField]: scene objects can't be serialized into a prefab,
    /// which is why the panels themselves have to be threaded through
    /// PlayerController.Initialize at runtime. An asset avoids that entirely.
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "New Gameplay Input Gate", menuName = "Jagara/Gameplay Input Gate")]
    public class GameplayInputGateSO : ScriptableObject
    {
        // Runtime-only: the gate must start open every Play Mode session, never
        // persist a stale block count into the asset.
        [NonSerialized] private int blockerCount;

        /// <summary>True while at least one UI has pushed a block.</summary>
        public bool IsBlocked => blockerCount > 0;

        public int BlockerCount => blockerCount;

        private void OnEnable() => blockerCount = 0;

        public void PushBlock() => blockerCount++;

        /// <summary>
        /// Releases one block. Clamps at 0 and logs an error on underflow -
        /// an unbalanced pop means some panel released a block it never took,
        /// which would let input through while a modal is still up. Mirrors
        /// TurnResolver.EndActorAnimation's handling of the same class of bug.
        /// </summary>
        public void PopBlock()
        {
            if (blockerCount <= 0)
            {
                Debug.LogError($"GameplayInputGateSO '{name}': PopBlock called with no matching PushBlock (blockerCount underflow). Clamping to 0.");
                blockerCount = 0;
                return;
            }

            blockerCount--;
        }

        /// <summary>
        /// Force-clears every outstanding block. Called when a floor starts, so a
        /// panel destroyed mid-transition can't leave the gate stuck closed.
        /// </summary>
        public void ResetGate() => blockerCount = 0;
    }
}
