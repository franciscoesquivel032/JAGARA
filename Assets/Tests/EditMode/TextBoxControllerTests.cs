using NUnit.Framework;
using TMPro;
using UnityEngine;
using Jagara.Runtime.Narrative;
using Jagara.Runtime.UI;

namespace Jagara.Tests.EditMode
{
    /// <summary>
    /// The text box is written to by four unrelated things (three menus and the
    /// action log), so the arbitration between them is the whole point of the
    /// class. These tests drive it through its public API and read back what the
    /// label actually ends up showing.
    ///
    /// The time-based clearing in Update() is not covered here: Edit Mode never
    /// pumps Update and Time.time does not advance, so that path belongs to
    /// manual Play Mode verification.
    /// </summary>
    public class TextBoxControllerTests
    {
        private GameObject go;
        private TMP_Text label;
        private CanvasGroup canvasGroup;
        private TextBoxController textBox;
        private MessageLogSO log;
        private MessageTemplateSO template;

        [SetUp]
        public void SetUp()
        {
            log = ScriptableObject.CreateInstance<MessageLogSO>();
            template = MessageTemplateSOTests.CreateTemplate("{0}", MessageCategory.Item);

            var labelGO = new GameObject("Label", typeof(RectTransform));
            label = labelGO.AddComponent<TextMeshProUGUI>();

            go = new GameObject("Text Box", typeof(RectTransform));
            canvasGroup = go.AddComponent<CanvasGroup>();
            textBox = go.AddComponent<TextBoxController>();
            MessageTemplateSOTests.SetPrivateField(textBox, "label", label);
            MessageTemplateSOTests.SetPrivateField(textBox, "messageLog", log);
            MessageTemplateSOTests.SetPrivateField(textBox, "canvasGroup", canvasGroup);
            MessageTemplateSOTests.SetPrivateField(textBox, "visibleLineCount", 3);

            // Outside Play Mode, AddComponent/SetActive do not invoke OnEnable for
            // a plain MonoBehaviour - the same limitation EnemyControllerTests
            // documents. Here the callback itself is what's under test (it is where
            // the log subscription lives), so it is invoked directly rather than
            // having its effects reproduced by hand.
            InvokeLifecycle("OnEnable");
        }

        [TearDown]
        public void TearDown()
        {
            InvokeLifecycle("OnDisable");
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(label.gameObject);
            Object.DestroyImmediate(log);
            Object.DestroyImmediate(template);
        }

        [Test]
        public void StartsEmptyAndHidden()
        {
            Assert.AreEqual(string.Empty, label.text);
            Assert.AreEqual(0f, canvasGroup.alpha, "with nothing to say the box must not be on screen");
        }

        [Test]
        public void BecomesVisibleWhenAMessageArrives()
        {
            log.Post(template, "You picked up the Old Compass.");

            Assert.AreEqual(1f, canvasGroup.alpha);
        }

        [Test]
        public void BecomesVisibleWhenAMenuSetsADescription()
        {
            // The box is shared with the menus, so a description has to show it
            // too - otherwise option descriptions would be invisible.
            textBox.SetDescription("Open the bag.");

            Assert.AreEqual(1f, canvasGroup.alpha);
        }

        [Test]
        public void HidesAgainOnceCleared()
        {
            log.Post(template, "a line");
            Assert.AreEqual(1f, canvasGroup.alpha);

            textBox.ClearDescription();

            Assert.AreEqual(0f, canvasGroup.alpha);
        }

        [Test]
        public void StaysVisibleWhileADescriptionIsShownWithNoMessages()
        {
            textBox.SetDescription("Open the bag.");
            log.Post(template, "a line");

            // Only the messages are forgotten here; the menu is still open, so the
            // box must stay up showing its description.
            Assert.AreEqual(1f, canvasGroup.alpha);
        }

        [Test]
        public void PostedMessage_AppearsWhenNoMenuIsDescribing()
        {
            log.Post(template, "You picked up the Nostalgia Fragment.");

            Assert.AreEqual("You picked up the Nostalgia Fragment.", label.text);
        }

        [Test]
        public void Messages_StackNewestLast()
        {
            log.Post(template, "one");
            log.Post(template, "two");

            Assert.AreEqual("one\ntwo", label.text);
        }

        [Test]
        public void Messages_BeyondVisibleLineCount_ShowOnlyTheNewest()
        {
            log.Post(template, "one");
            log.Post(template, "two");
            log.Post(template, "three");
            log.Post(template, "four");

            Assert.AreEqual("two\nthree\nfour", label.text);
        }

        [Test]
        public void SetDescription_TakesPriorityOverTheLog()
        {
            log.Post(template, "a log line");

            textBox.SetDescription("Use the selected item.");

            Assert.AreEqual("Use the selected item.", label.text);
        }

        [Test]
        public void MessagePostedWhileMenuIsOpen_DoesNotOverwriteTheDescription()
        {
            textBox.SetDescription("Use the selected item.");

            log.Post(template, "You used the Nostalgia Fragment.");

            Assert.AreEqual("Use the selected item.", label.text);
        }

        [Test]
        public void ClearDescription_ForgetsMessagesPostedWhileTheMenuWasOpen()
        {
            textBox.SetDescription("Use the selected item.");
            log.Post(template, "You used the Nostalgia Fragment.");

            textBox.ClearDescription();

            Assert.AreEqual(string.Empty, label.text,
                "clearing the box discards what was on it - stale lines must not come back");
        }

        [Test]
        public void ClearDescription_WithNoMessages_LeavesTheBoxEmpty()
        {
            textBox.SetDescription("Open the bag.");

            textBox.ClearDescription();

            Assert.AreEqual(string.Empty, label.text);
        }

        [Test]
        public void MessagesPostedAfterAClear_DoNotDragBackTheForgottenOnes()
        {
            log.Post(template, "old one");
            log.Post(template, "old two");
            textBox.ClearDescription();

            log.Post(template, "fresh");

            Assert.AreEqual("fresh", label.text,
                "the window restarts after a clear rather than re-reading the log's history");
        }

        [Test]
        public void SetDescription_WithEmptyString_BlanksTheBoxRatherThanShowingTheLog()
        {
            log.Post(template, "a log line");

            // An empty description is a menu deliberately showing nothing - it is
            // not the same as having no description at all.
            textBox.SetDescription(string.Empty);

            Assert.AreEqual(string.Empty, label.text);
        }

        [Test]
        public void CategoryColor_WrapsMatchingMessagesInARichTextTag()
        {
            MessageTemplateSOTests.SetPrivateField(textBox, "categoryColors",
                CreateCategoryColors((MessageCategory.Item, Color.red)));

            log.Post(template, "tinted");

            Assert.AreEqual("<color=#FF0000>tinted</color>", label.text);
        }

        [Test]
        public void CategoryColor_LeavesUnlistedCategoriesUntinted()
        {
            MessageTemplateSOTests.SetPrivateField(textBox, "categoryColors",
                CreateCategoryColors((MessageCategory.Combat, Color.red)));

            log.Post(template, "plain");

            Assert.AreEqual("plain", label.text);
        }

        private void InvokeLifecycle(string methodName)
        {
            var method = typeof(TextBoxController).GetMethod(methodName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(method, $"TextBoxController.{methodName} not found");
            method.Invoke(textBox, null);
        }

        /// <summary>
        /// TextBoxController.CategoryColor is a private nested struct, so the test
        /// builds the array reflectively rather than duplicating its shape. It has
        /// to be a correctly typed array, not object[], for the field assignment
        /// to take.
        /// </summary>
        private static System.Array CreateCategoryColors(params (MessageCategory category, Color color)[] entries)
        {
            var type = typeof(TextBoxController).GetNestedType("CategoryColor",
                System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(type, "TextBoxController.CategoryColor not found");

            System.Array array = System.Array.CreateInstance(type, entries.Length);
            for (int i = 0; i < entries.Length; i++)
            {
                object entry = System.Activator.CreateInstance(type);
                type.GetField("category").SetValue(entry, entries[i].category);
                type.GetField("color").SetValue(entry, entries[i].color);
                array.SetValue(entry, i);
            }

            return array;
        }
    }
}
