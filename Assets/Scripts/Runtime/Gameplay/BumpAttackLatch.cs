using UnityEngine;

namespace Jagara.Runtime.Gameplay
{
    /// <summary>
    /// Enforces "one bump-attack per press" on directional input, keeping attacking
    /// and walking two distinct committed actions.
    /// <para>
    /// Walking is self-limiting: GridMover holds IsMoving for the whole step tween,
    /// so a held direction produces at most one step per moveDuration. A bump-attack
    /// resolves instantly with no tween, so without this latch one held key commits
    /// an attack - and therefore a whole turn, enemy retaliation included - every
    /// frame, and then walks the player into the tile the killed enemy just vacated.
    /// </para>
    /// <para>
    /// The latch is per-direction rather than a blanket "any input" block: turning
    /// away from an enemy responds on the very next frame instead of forcing a key
    /// release the player never asked for. Pure C# - it only ever sees an
    /// already-resolved cardinal direction, so the rule is testable in Edit Mode.
    /// </para>
    /// </summary>
    public class BumpAttackLatch
    {
        // Vector2Int.zero means "not latched". A latch always holds one of the four
        // cardinals (that is all CardinalDirectionResolver ever produces), so zero
        // is unambiguous as a sentinel.
        private Vector2Int latchedDirection;

        /// <summary>
        /// True while input is still held in the direction the last bump-attack was
        /// committed on - nothing else may be committed until it changes or releases.
        /// </summary>
        public bool IsHeld => latchedDirection != Vector2Int.zero;

        /// <summary>
        /// Records this frame's input direction, releasing the latch as soon as the
        /// direction changes or returns to neutral. Must be called EVERY frame,
        /// including frames the player cannot act on (mid-tween, turn resolving,
        /// menu open) - a key released during one of those frames would otherwise
        /// never be seen, and the player's next press would be swallowed.
        /// </summary>
        public void Observe(Vector2Int direction)
        {
            if (direction != latchedDirection)
            {
                latchedDirection = Vector2Int.zero;
            }
        }

        /// <summary>Latches after a bump-attack has been committed in <paramref name="direction"/>.</summary>
        public void Latch(Vector2Int direction)
        {
            latchedDirection = direction;
        }

        /// <summary>Drops the latch outright, for state resets (e.g. the player being placed on a new floor).</summary>
        public void Clear()
        {
            latchedDirection = Vector2Int.zero;
        }
    }
}
