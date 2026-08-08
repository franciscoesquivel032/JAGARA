using NUnit.Framework;
using UnityEngine;
using Jagara.Runtime.Gameplay;

namespace Jagara.Tests.EditMode
{
    /// <summary>
    /// The rule these cover is what keeps a bump-attack a single action: an attack
    /// resolves with no tween, so unless the direction it was committed on is
    /// latched until release, PlayerController.Update commits one attack - and one
    /// whole turn of enemy retaliation - per frame, then walks the player into the
    /// tile the dead enemy vacated.
    /// </summary>
    public class BumpAttackLatchTests
    {
        private BumpAttackLatch latch;

        [SetUp]
        public void SetUp()
        {
            latch = new BumpAttackLatch();
        }

        [Test]
        public void FreshLatch_IsNotHeld()
        {
            Assert.IsFalse(latch.IsHeld);
        }

        [Test]
        public void Observing_WithoutAnyAttack_NeverHolds()
        {
            latch.Observe(Vector2Int.left);
            Assert.IsFalse(latch.IsHeld, "Walking must never be gated by the attack latch.");

            latch.Observe(Vector2Int.left);
            Assert.IsFalse(latch.IsHeld, "Holding a direction to walk must keep stepping.");
        }

        [Test]
        public void AfterAttack_SameDirectionStillHeld_StaysLatched()
        {
            latch.Latch(Vector2Int.left);

            latch.Observe(Vector2Int.left);

            Assert.IsTrue(latch.IsHeld, "A held key must not commit a second attack, nor a step into the freed tile.");
        }

        [Test]
        public void AfterAttack_ManyFramesOfSameDirection_StayLatched()
        {
            latch.Latch(Vector2Int.up);

            for (int frame = 0; frame < 60; frame++)
            {
                latch.Observe(Vector2Int.up);
            }

            Assert.IsTrue(latch.IsHeld, "One press must stay one action however long it is held.");
        }

        [Test]
        public void AfterAttack_InputReleased_ReleasesLatch()
        {
            latch.Latch(Vector2Int.left);
            latch.Observe(Vector2Int.left);

            latch.Observe(Vector2Int.zero);

            Assert.IsFalse(latch.IsHeld, "Releasing the key must re-arm input for the next press.");
        }

        [Test]
        public void AfterAttack_ReleasedThenPressedAgain_AllowsASecondAttack()
        {
            latch.Latch(Vector2Int.left);
            latch.Observe(Vector2Int.zero);

            latch.Observe(Vector2Int.left);

            Assert.IsFalse(latch.IsHeld, "A deliberate second press must be able to attack again.");
        }

        [Test]
        public void AfterAttack_DifferentDirection_ReleasesImmediately()
        {
            latch.Latch(Vector2Int.left);

            latch.Observe(Vector2Int.up);

            Assert.IsFalse(latch.IsHeld, "Turning away from the enemy must respond on the next frame, not wait for a release.");
        }

        [Test]
        public void Clear_ReleasesLatch()
        {
            latch.Latch(Vector2Int.right);

            latch.Clear();

            Assert.IsFalse(latch.IsHeld);
        }
    }
}
