using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Jagara.Runtime.TurnSystem;

namespace Jagara.Tests.EditMode
{
    public class TurnResolverTests
    {
        private class RecordingActor : ITurnActor
        {
            public int Id;
            public List<int> Log;
            public void TakeTurn() => Log.Add(Id);
        }

        /// <summary>Unregisters itself from the resolver from within its own TakeTurn.</summary>
        private class SelfUnregisteringActor : ITurnActor
        {
            public int Id;
            public List<int> Log;
            public TurnResolver Resolver;
            public void TakeTurn()
            {
                Log.Add(Id);
                Resolver.UnregisterActor(this);
            }
        }

        /// <summary>Records the resolver's IsResolving value observed during TakeTurn.</summary>
        private class ResolvingObserverActor : ITurnActor
        {
            public TurnResolver Resolver;
            public bool ObservedIsResolving;
            public void TakeTurn() => ObservedIsResolving = Resolver.IsResolving;
        }

        [Test]
        public void EndPlayerTurn_CallsRegisteredActorsInRegistrationOrder()
        {
            var resolver = new TurnResolver();
            var log = new List<int>();
            resolver.RegisterActor(new RecordingActor { Id = 1, Log = log });
            resolver.RegisterActor(new RecordingActor { Id = 2, Log = log });

            resolver.EndPlayerTurn();

            CollectionAssert.AreEqual(new[] { 1, 2 }, log);
        }

        [Test]
        public void EndPlayerTurn_WithNoRegisteredActors_DoesNotThrow()
        {
            var resolver = new TurnResolver();
            Assert.DoesNotThrow(() => resolver.EndPlayerTurn());
        }

        [Test]
        public void UnregisterActor_PreventsFurtherTakeTurnCalls()
        {
            var resolver = new TurnResolver();
            var log = new List<int>();
            var actor = new RecordingActor { Id = 1, Log = log };
            resolver.RegisterActor(actor);
            resolver.UnregisterActor(actor);

            resolver.EndPlayerTurn();

            CollectionAssert.IsEmpty(log);
        }

        [Test]
        public void TurnNumber_IncrementsAtStartOfEachEndPlayerTurn()
        {
            var resolver = new TurnResolver();
            Assert.AreEqual(0, resolver.TurnNumber);

            resolver.EndPlayerTurn();
            Assert.AreEqual(1, resolver.TurnNumber);

            resolver.EndPlayerTurn();
            Assert.AreEqual(2, resolver.TurnNumber);
        }

        [Test]
        public void TurnNumber_IsAlreadyIncremented_WhileActorsAreTakingTheirTurn()
        {
            var resolver = new TurnResolver();
            int? observedTurnNumberDuringTakeTurn = null;
            var actor = new ActionActor(() => observedTurnNumberDuringTakeTurn = resolver.TurnNumber);
            resolver.RegisterActor(actor);

            resolver.EndPlayerTurn();

            Assert.AreEqual(1, observedTurnNumberDuringTakeTurn);
        }

        [Test]
        public void IsResolving_IsFalse_BeforeAndAfterEndPlayerTurn()
        {
            var resolver = new TurnResolver();
            Assert.IsFalse(resolver.IsResolving);

            resolver.EndPlayerTurn();

            Assert.IsFalse(resolver.IsResolving);
        }

        [Test]
        public void IsResolving_IsTrue_WhileActorLoopIsIterating()
        {
            var resolver = new TurnResolver();
            var observer = new ResolvingObserverActor { Resolver = resolver };
            resolver.RegisterActor(observer);

            resolver.EndPlayerTurn();

            Assert.IsTrue(observer.ObservedIsResolving);
        }

        [Test]
        public void IsResolving_IsTrue_WhileAnAnimationIsInFlightAfterTheLoopEnds()
        {
            var resolver = new TurnResolver();
            resolver.EndPlayerTurn();

            resolver.BeginActorAnimation();

            Assert.IsTrue(resolver.IsResolving);
        }

        [Test]
        public void IsResolving_ReturnsFalse_OnceAllInFlightAnimationsEnd()
        {
            var resolver = new TurnResolver();
            resolver.BeginActorAnimation();
            resolver.BeginActorAnimation();

            resolver.EndActorAnimation();
            Assert.IsTrue(resolver.IsResolving);

            resolver.EndActorAnimation();
            Assert.IsFalse(resolver.IsResolving);
        }

        [Test]
        public void BeginAndEndActorAnimation_PairAcrossMultipleActors()
        {
            var resolver = new TurnResolver();

            resolver.BeginActorAnimation();
            resolver.BeginActorAnimation();
            resolver.BeginActorAnimation();
            Assert.IsTrue(resolver.IsResolving);

            resolver.EndActorAnimation();
            resolver.EndActorAnimation();
            Assert.IsTrue(resolver.IsResolving);

            resolver.EndActorAnimation();
            Assert.IsFalse(resolver.IsResolving);
        }

        [Test]
        public void EndActorAnimation_PastZero_ClampsToZeroAndLogsError()
        {
            var resolver = new TurnResolver();

            LogAssert.Expect(LogType.Error, new Regex("EndActorAnimation"));
            resolver.EndActorAnimation();

            Assert.IsFalse(resolver.IsResolving);

            // A subsequent legitimate Begin/End pair still works correctly after the clamp.
            resolver.BeginActorAnimation();
            Assert.IsTrue(resolver.IsResolving);
            resolver.EndActorAnimation();
            Assert.IsFalse(resolver.IsResolving);
        }

        [Test]
        public void SelfUnregisteringActor_DoesNotCauseNextActorToBeSkippedThisTurn()
        {
            var resolver = new TurnResolver();
            var log = new List<int>();
            var selfUnregistering = new SelfUnregisteringActor { Id = 1, Log = log, Resolver = resolver };
            var nextActor = new RecordingActor { Id = 2, Log = log };
            resolver.RegisterActor(selfUnregistering);
            resolver.RegisterActor(nextActor);

            resolver.EndPlayerTurn();

            CollectionAssert.AreEqual(new[] { 1, 2 }, log);
        }

        [Test]
        public void SelfUnregisteringActor_DoesNotTakeATurnAgainAfterwards()
        {
            var resolver = new TurnResolver();
            var log = new List<int>();
            var selfUnregistering = new SelfUnregisteringActor { Id = 1, Log = log, Resolver = resolver };
            var nextActor = new RecordingActor { Id = 2, Log = log };
            resolver.RegisterActor(selfUnregistering);
            resolver.RegisterActor(nextActor);

            resolver.EndPlayerTurn();
            log.Clear();
            resolver.EndPlayerTurn();

            CollectionAssert.AreEqual(new[] { 2 }, log);
        }

        [Test]
        public void RegisterActor_DuringIteration_DoesNotTakeATurnUntilTheNextEndPlayerTurn()
        {
            var resolver = new TurnResolver();
            var log = new List<int>();
            var lateRegistered = new RecordingActor { Id = 2, Log = log };
            var registeringActor = new ActionActor(() => resolver.RegisterActor(lateRegistered));
            resolver.RegisterActor(registeringActor);

            resolver.EndPlayerTurn();
            CollectionAssert.IsEmpty(log);

            resolver.EndPlayerTurn();
            CollectionAssert.AreEqual(new[] { 2 }, log);
        }

        /// <summary>Runs an arbitrary delegate as its TakeTurn, for tests that need a one-off actor.</summary>
        private class ActionActor : ITurnActor
        {
            private readonly System.Action action;
            public ActionActor(System.Action action) => this.action = action;
            public void TakeTurn() => action();
        }
    }
}
