using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Jagara.Runtime.Data;

namespace Jagara.Tests.EditMode
{
    /// <summary>
    /// The gate is what stops the player walking while a menu or dialogue is up,
    /// so an unbalanced push/pop is either a soft-lock (stuck blocked) or input
    /// leaking through a modal. Both directions are covered here.
    /// </summary>
    public class GameplayInputGateSOTests
    {
        private GameplayInputGateSO gate;

        [SetUp]
        public void SetUp()
        {
            gate = ScriptableObject.CreateInstance<GameplayInputGateSO>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(gate);
        }

        [Test]
        public void StartsUnblocked()
        {
            Assert.IsFalse(gate.IsBlocked);
            Assert.AreEqual(0, gate.BlockerCount);
        }

        [Test]
        public void PushThenPop_ReturnsToUnblocked()
        {
            gate.PushBlock();
            Assert.IsTrue(gate.IsBlocked);

            gate.PopBlock();
            Assert.IsFalse(gate.IsBlocked);
        }

        [Test]
        public void NestedBlocks_StayBlockedUntilEveryPopRuns()
        {
            // The real case: the action menu is open (one block) and a dialogue
            // opens on top of it (a second block).
            gate.PushBlock();
            gate.PushBlock();

            gate.PopBlock();
            Assert.IsTrue(gate.IsBlocked, "one holder released, but the other is still open");

            gate.PopBlock();
            Assert.IsFalse(gate.IsBlocked);
        }

        [Test]
        public void PopWithoutPush_LogsErrorAndClampsToZero()
        {
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("blockerCount underflow"));

            gate.PopBlock();

            Assert.AreEqual(0, gate.BlockerCount, "underflow must clamp, never go negative");
            Assert.IsFalse(gate.IsBlocked);
        }

        [Test]
        public void PopAfterUnderflow_DoesNotLeaveGateStuckOpen()
        {
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("blockerCount underflow"));
            gate.PopBlock();

            // A negative count would have made this push fail to block.
            gate.PushBlock();
            Assert.IsTrue(gate.IsBlocked);
        }

        [Test]
        public void ResetGate_ClearsEveryOutstandingBlock()
        {
            gate.PushBlock();
            gate.PushBlock();

            gate.ResetGate();

            Assert.IsFalse(gate.IsBlocked);
            Assert.AreEqual(0, gate.BlockerCount);
        }
    }
}
