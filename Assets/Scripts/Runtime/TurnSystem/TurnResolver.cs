using System.Collections.Generic;

namespace Jagara.Runtime.TurnSystem
{
    /// <summary>
    /// Orchestrates turn order: once the player's committed action has fully
    /// resolved, every registered ITurnActor takes its turn in registration order.
    /// Fully synchronous today because no registered actor has async/animated
    /// behavior yet - once enemies need their own tweened actions, this will need
    /// to become coroutine/async-driven so the player can wait for the enemy phase.
    /// </summary>
    public class TurnResolver
    {
        private readonly List<ITurnActor> otherActors = new();

        public void RegisterActor(ITurnActor actor) => otherActors.Add(actor);

        public void UnregisterActor(ITurnActor actor) => otherActors.Remove(actor);

        public void EndPlayerTurn()
        {
            for (int i = 0; i < otherActors.Count; i++)
            {
                otherActors[i].TakeTurn();
            }
        }
    }
}
