using UnityEngine;

namespace Jagara.Runtime.UI
{
    /// <summary>
    /// Pure HP-delta -> popup text/colour mapping, kept separate from
    /// HpPopupBinder so the formatting rules are testable without Unity's
    /// MonoBehaviour lifecycle.
    /// </summary>
    public static class HpPopupFormatter
    {
        /// <summary>Heal popup colour - #4CAF50, standard green. Same for every entity - only the damage colour differs by who took the hit.</summary>
        public static readonly Color HealColor = new Color(0.2980392f, 0.6862745f, 0.3137255f);

        /// <summary>
        /// Formats an HP delta as popup text/colour. <paramref name="damageColor"/>
        /// is supplied by the caller (HpPopupBinder) rather than fixed here,
        /// since the same negative delta reads differently depending on who
        /// took the hit - white on an enemy (the player's own damage), red on
        /// the player (an enemy's damage). Returns null for a zero delta -
        /// callers must not spawn a popup in that case.
        /// </summary>
        public static (string Text, Color Color)? Format(int delta, Color damageColor)
        {
            if (delta < 0)
            {
                return ($"-{-delta}", damageColor);
            }

            if (delta > 0)
            {
                return ($"+{delta}", HealColor);
            }

            return null;
        }
    }
}
