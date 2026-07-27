namespace Jagara.Runtime.TurnSystem
{
    /// <summary>
    /// An entity that automatically acts once per turn once it's the player's turn
    /// to resolve (e.g. enemies). The player is input-driven and does not implement
    /// this - see TurnResolver.EndPlayerTurn.
    /// </summary>
    public interface ITurnActor
    {
        void TakeTurn();
    }
}
