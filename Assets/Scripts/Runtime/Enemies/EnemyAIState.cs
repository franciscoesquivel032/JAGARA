namespace Jagara.Runtime.Enemies
{
    /// <summary>
    /// High-level behavior state for an enemy's AI. Dormant enemies do not chase
    /// the player; Chasing enemies path toward the player via
    /// <see cref="EnemyAILogic.TryChooseStep"/>. Transitions between the two are
    /// decided by <see cref="EnemyAILogic.ShouldWake"/> / <see cref="EnemyAILogic.ShouldForget"/>,
    /// driven by a later task's EnemyController - this enum carries no behavior
    /// of its own.
    /// </summary>
    public enum EnemyAIState
    {
        Dormant,
        Chasing,
    }
}
