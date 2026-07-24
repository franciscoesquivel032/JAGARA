using System.Collections.Generic;
using NUnit.Framework;
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
    }
}
