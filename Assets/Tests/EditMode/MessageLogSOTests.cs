using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Jagara.Runtime.Narrative;

namespace Jagara.Tests.EditMode
{
    /// <summary>
    /// The log's ring buffer is what keeps a long play session from growing an
    /// unbounded message list, and Recent() is read every time a message posts,
    /// so both its wrap-around indexing and its ordering are worth pinning down.
    /// </summary>
    public class MessageLogSOTests
    {
        private MessageLogSO log;
        private MessageTemplateSO template;

        [SetUp]
        public void SetUp()
        {
            log = ScriptableObject.CreateInstance<MessageLogSO>();
            template = MessageTemplateSOTests.CreateTemplate("{0}", MessageCategory.Item);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(log);
            Object.DestroyImmediate(template);
        }

        [Test]
        public void Recent_ReturnsMessagesOldestFirst()
        {
            Post("a", "b", "c");

            CollectionAssert.AreEqual(new[] { "a", "b", "c" }, TextsOf(log.Recent(3)));
        }

        [Test]
        public void Recent_MoreThanLogged_ReturnsEverythingHeld()
        {
            Post("a", "b");

            CollectionAssert.AreEqual(new[] { "a", "b" }, TextsOf(log.Recent(10)));
        }

        [Test]
        public void Recent_FewerThanLogged_ReturnsTheNewestOnes()
        {
            Post("a", "b", "c", "d");

            CollectionAssert.AreEqual(new[] { "c", "d" }, TextsOf(log.Recent(2)));
        }

        [Test]
        public void Recent_ZeroOrNegative_ReturnsNothing()
        {
            Post("a");

            Assert.AreEqual(0, log.Recent(0).Count);
            Assert.AreEqual(0, log.Recent(-3).Count);
        }

        [Test]
        public void Post_BeyondCapacity_DiscardsOldestAndKeepsOrder()
        {
            SetCapacity(3);

            Post("a", "b", "c", "d", "e");

            Assert.AreEqual(3, log.Count, "count must stay clamped to capacity");
            CollectionAssert.AreEqual(new[] { "c", "d", "e" }, TextsOf(log.Recent(3)));
        }

        [Test]
        public void Post_AfterWrapAround_RecentStillIndexesCorrectly()
        {
            SetCapacity(3);
            Post("a", "b", "c", "d");

            // "b","c","d" are held, but "b" now sits physically after "d" in the buffer.
            CollectionAssert.AreEqual(new[] { "c", "d" }, TextsOf(log.Recent(2)));
        }

        [Test]
        public void Post_RaisesOnMessagePostedOncePerMessage()
        {
            var received = new List<GameMessage>();
            log.OnMessagePosted += received.Add;

            Post("a", "b");

            Assert.AreEqual(2, received.Count);
            Assert.AreEqual("a", received[0].Text);
            Assert.AreEqual(MessageCategory.Item, received[0].Category);
        }

        [Test]
        public void Post_NullTemplate_LogsErrorAndPostsNothing()
        {
            UnityEngine.TestTools.LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("null template"));

            log.Post(null, "x");

            Assert.AreEqual(0, log.Count);
        }

        [Test]
        public void Post_TintsItemNames()
        {
            var pickedUp = MessageTemplateSOTests.CreateTemplate("You picked up the {0}.", MessageCategory.Item);

            log.Post(pickedUp, StyledName.Item("Old Compass"));

            Assert.AreEqual("You picked up the <color=#FDC090>Old Compass</color>.", log.Recent(1)[0].Text);

            Object.DestroyImmediate(pickedUp);
        }

        [Test]
        public void Post_TintsEnemyNames()
        {
            var damage = MessageTemplateSOTests.CreateTemplate("{0} takes {1} damage.", MessageCategory.Combat);

            log.Post(damage, StyledName.Enemy("Leecher"), 4);

            Assert.AreEqual("<color=#FF5D61>Leecher</color> takes 4 damage.", log.Recent(1)[0].Text);

            Object.DestroyImmediate(damage);
        }

        [Test]
        public void Post_TintsEachStyledNameIndependently()
        {
            var ability = MessageTemplateSOTests.CreateTemplate("{0} uses the {1}!", MessageCategory.Combat);

            log.Post(ability, StyledName.Enemy("Abomination"), StyledName.Item("Blood Vial"));

            Assert.AreEqual(
                "<color=#FF5D61>Abomination</color> uses the <color=#FDC090>Blood Vial</color>!",
                log.Recent(1)[0].Text);

            Object.DestroyImmediate(ability);
        }

        [Test]
        public void Post_LeavesPlainArgumentsUntinted()
        {
            var mixed = MessageTemplateSOTests.CreateTemplate("{0} and {1}.", MessageCategory.System);

            log.Post(mixed, "a plain string", 7);

            Assert.AreEqual("a plain string and 7.", log.Recent(1)[0].Text);

            Object.DestroyImmediate(mixed);
        }

        [Test]
        public void Post_RoleWithNoPaletteEntry_RendersNameUntinted()
        {
            MessageTemplateSOTests.SetPrivateField(log, "nameColors", System.Array.CreateInstance(
                typeof(MessageLogSO).GetNestedType("NameRoleColor", BindingFlags.NonPublic), 0));

            log.Post(template, StyledName.Item("Old Compass"));

            Assert.AreEqual("Old Compass", log.Recent(1)[0].Text,
                "an unmapped role must still read correctly, just without colour");
        }

        [Test]
        public void PostRaw_AddsMessageWithGivenCategory()
        {
            log.PostRaw("straight to the log", MessageCategory.System);

            IReadOnlyList<GameMessage> recent = log.Recent(1);
            Assert.AreEqual(1, recent.Count);
            Assert.AreEqual("straight to the log", recent[0].Text);
            Assert.AreEqual(MessageCategory.System, recent[0].Category);
        }

        [Test]
        public void Clear_EmptiesHistory()
        {
            Post("a", "b");

            log.Clear();

            Assert.AreEqual(0, log.Count);
            Assert.AreEqual(0, log.Recent(5).Count);
        }

        [Test]
        public void Clear_ThenPost_StartsFromScratch()
        {
            Post("a", "b");
            log.Clear();
            Post("c");

            CollectionAssert.AreEqual(new[] { "c" }, TextsOf(log.Recent(5)));
        }

        private void Post(params string[] texts)
        {
            foreach (string text in texts)
            {
                log.Post(template, text);
            }
        }

        private static string[] TextsOf(IReadOnlyList<GameMessage> messages)
        {
            var texts = new string[messages.Count];
            for (int i = 0; i < messages.Count; i++)
            {
                texts[i] = messages[i].Text;
            }
            return texts;
        }

        /// <summary>
        /// Resizes the ring buffer. CreateInstance already ran OnEnable with the
        /// default capacity, so the lifecycle method has to be re-run by hand for
        /// the new value to take effect.
        /// </summary>
        private void SetCapacity(int capacity)
        {
            MessageTemplateSOTests.SetPrivateField(log, "capacity", capacity);

            var onEnable = typeof(MessageLogSO).GetMethod("OnEnable", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(onEnable, "MessageLogSO.OnEnable not found");
            onEnable.Invoke(log, null);
        }
    }
}
