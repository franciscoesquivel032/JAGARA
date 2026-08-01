using System;

namespace Jagara.Runtime.Resources
{
    /// <summary>
    /// Runtime Paranoia for the player (player-only resource per the GDD).
    /// Pure C#, owned by PlayerStatsSO. OnParanoiaMaxed is a placeholder hook -
    /// [PENDIENTE] per the GDD, it currently has no gameplay effect beyond
    /// firing once when Paranoia first reaches Max.
    /// </summary>
    public class ParanoiaState
    {
        public int Current { get; private set; }
        public int Max { get; }

        public bool IsMaxed => Current >= Max;

        public event Action<int, int> OnParanoiaChanged = delegate { };
        public event Action OnParanoiaMaxed = delegate { };

        private bool maxedFired;

        public ParanoiaState(int max)
        {
            Max = max;
        }

        /// <summary>Applies a Paranoia gain, clamped at Max. No-ops if already maxed or amount is non-positive.</summary>
        public void Gain(int amount)
        {
            if (amount <= 0 || IsMaxed)
            {
                return;
            }

            Current = Math.Min(Max, Current + amount);
            OnParanoiaChanged(Current, Max);

            if (IsMaxed && !maxedFired)
            {
                maxedFired = true;
                OnParanoiaMaxed();
            }
        }
    }
}
