using System.Collections.Generic;
using UnityEngine;

namespace Jagara.Runtime.TurnSystem
{
    /// <summary>
    /// Orchestrates turn order: once the player's committed action has fully
    /// resolved, every registered ITurnActor takes its turn in registration order.
    /// Grid resolution itself stays fully synchronous (deterministic, testable) -
    /// but enemy actors may kick off tweened animations that outlive their
    /// TakeTurn() call, so this also tracks an animation gate (see
    /// BeginActorAnimation/EndActorAnimation) that callers (e.g. PlayerController)
    /// can use to block further input until every in-flight enemy tween settles.
    /// </summary>
    public class TurnResolver
    {
        private readonly List<ITurnActor> otherActors = new();
        private readonly List<ITurnActor> pendingRegistrations = new();
        private readonly List<ITurnActor> pendingUnregistrations = new();

        private bool isIterating;
        private int animatingActorCount;

        /// <summary>Number of player turns resolved so far. Increments at the start of EndPlayerTurn.</summary>
        public int TurnNumber { get; private set; }

        /// <summary>
        /// True while the actor loop is iterating, or while one or more actors have an
        /// in-flight animation (see BeginActorAnimation/EndActorAnimation). Consumers
        /// (e.g. PlayerController.Update) should block new player input while this is true.
        /// </summary>
        public bool IsResolving => isIterating || animatingActorCount > 0;

        /// <summary>
        /// Registers an actor to take a turn each time EndPlayerTurn runs. If called while
        /// the actor loop is currently iterating, the registration is deferred until the
        /// loop finishes - the newly registered actor will not act this turn.
        /// </summary>
        public void RegisterActor(ITurnActor actor)
        {
            if (isIterating)
            {
                pendingRegistrations.Add(actor);
            }
            else
            {
                otherActors.Add(actor);
            }
        }

        /// <summary>
        /// Unregisters an actor so it no longer takes turns. If called while the actor loop
        /// is currently iterating (e.g. an actor unregistering itself from within its own
        /// TakeTurn), the removal is deferred until the loop finishes so the list isn't
        /// mutated mid-iteration - otherwise the actor immediately after the removed one
        /// would be skipped for the rest of this turn.
        /// </summary>
        public void UnregisterActor(ITurnActor actor)
        {
            if (isIterating)
            {
                pendingUnregistrations.Add(actor);
            }
            else
            {
                otherActors.Remove(actor);
            }
        }

        public void EndPlayerTurn()
        {
            TurnNumber++;

            isIterating = true;
            for (int i = 0; i < otherActors.Count; i++)
            {
                otherActors[i].TakeTurn();
            }
            isIterating = false;

            FlushPendingRegistrations();
        }

        private void FlushPendingRegistrations()
        {
            for (int i = 0; i < pendingUnregistrations.Count; i++)
            {
                otherActors.Remove(pendingUnregistrations[i]);
            }
            pendingUnregistrations.Clear();

            for (int i = 0; i < pendingRegistrations.Count; i++)
            {
                otherActors.Add(pendingRegistrations[i]);
            }
            pendingRegistrations.Clear();
        }

        /// <summary>Marks one actor's animation as in-flight, holding IsResolving true.</summary>
        public void BeginActorAnimation() => animatingActorCount++;

        /// <summary>
        /// Marks one actor's animation as finished. Safe to call from arbitrary
        /// MonoBehaviour lifecycle points (e.g. an actor's OnDisable if it's destroyed
        /// mid-tween), not just from within the resolver's own loop. Clamps at 0 and
        /// logs an error if called more times than BeginActorAnimation - that mismatch
        /// is a real bug (a soft-locked IsResolving gate), not an expected state.
        /// </summary>
        public void EndActorAnimation()
        {
            if (animatingActorCount <= 0)
            {
                Debug.LogError("TurnResolver.EndActorAnimation called with no matching BeginActorAnimation (animatingActorCount underflow). Clamping to 0.");
                animatingActorCount = 0;
                return;
            }

            animatingActorCount--;
        }
    }
}
