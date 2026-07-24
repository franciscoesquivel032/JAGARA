using UnityEngine;

namespace Jagara.Runtime.Gameplay
{
    /// <summary>
    /// Reduces a raw Vector2 input reading to a single cardinal grid direction.
    /// No diagonals: if both axes exceed the deadzone at once, vertical wins.
    /// </summary>
    public static class CardinalDirectionResolver
    {
        public static Vector2Int Resolve(Vector2 raw, float deadzone = 0.5f)
        {
            if (raw.y > deadzone) return Vector2Int.up;
            if (raw.y < -deadzone) return Vector2Int.down;
            if (raw.x > deadzone) return Vector2Int.right;
            if (raw.x < -deadzone) return Vector2Int.left;
            return Vector2Int.zero;
        }
    }
}
