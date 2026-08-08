namespace Jagara.Runtime.Combat
{
    /// <summary>
    /// Pure stat -> resource formulas for the player, who is the only actor
    /// whose HP and damage derive from stat points (allocated in the hub).
    /// Enemies do not use these: their HP and damage are authored directly on
    /// EnemyConfigSO so each type can be hand-tuned without moving every other
    /// enemy at once. Tuning constants are [PROVISIONAL] placeholders (not
    /// GDD-specified numbers) - kept in one place so they're trivial to retune
    /// later. There is no ability/PP system yet, so BaseAttackDamage stands in
    /// for "base damage of the basic bump-attack" until one exists.
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
