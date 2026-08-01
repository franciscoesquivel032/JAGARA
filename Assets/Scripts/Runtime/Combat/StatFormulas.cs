namespace Jagara.Runtime.Combat
{
    /// <summary>
    /// Pure stat -> resource formulas shared by player and enemies. Tuning
    /// constants are [PROVISIONAL] placeholders (not GDD-specified numbers) -
    /// kept in one place so they're trivial to retune later. There is no
    /// ability/PP system yet, so BaseAttackDamage stands in for "base damage
    /// of the basic bump-attack" until one exists.
    /// </summary>
    public static class StatFormulas
    {
        private const int BaseHP = 20;
        private const int HPPerVigorPoint = 5;

        private const int BaseAttackDamage = 2;
        private const int DamagePerPoderPoint = 1;

        public static int ComputeMaxHP(int vigor) => BaseHP + vigor * HPPerVigorPoint;

        public static int ComputeAttackDamage(int poder) => BaseAttackDamage + poder * DamagePerPoderPoint;
    }
}
