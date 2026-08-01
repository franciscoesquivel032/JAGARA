using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Jagara.Runtime.Narrative;

namespace Jagara.Tests.EditMode
{
    /// <summary>
    /// MessageFormatter is the one place where authored template text meets
    /// runtime values, so it is also the one place a mis-authored asset can
    /// surface. These tests pin down that a mismatch degrades to visible-but-safe
    /// output instead of throwing: a FormatException raised here would abort turn
    /// resolution partway through.
    /// </summary>
    public class MessageFormatterTests
    {
        [Test]
        public void Format_SubstitutesPositionalArguments()
        {
            Assert.AreEqual("Shade takes 4 damage.", MessageFormatter.Format("{0} takes {1} damage.", "Shade", 4));
        }

        [Test]
        public void Format_TemplateWithoutPlaceholders_IgnoresArguments()
        {
            Assert.AreEqual("You sink deeper.", MessageFormatter.Format("You sink deeper.", "unused"));
        }

        [Test]
        public void Format_NoArguments_ReturnsTemplateUnchanged()
        {
            Assert.AreEqual("{0} takes damage.", MessageFormatter.Format("{0} takes damage."));
        }

        [Test]
        public void Format_NullOrEmptyTemplate_ReturnsEmptyString()
        {
            Assert.AreEqual(string.Empty, MessageFormatter.Format(null, "x"));
            Assert.AreEqual(string.Empty, MessageFormatter.Format(string.Empty, "x"));
        }

        [Test]
        public void Format_TooFewArguments_LogsErrorAndReturnsRawTemplate()
        {
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("does not match its 1 argument"));

            Assert.AreEqual("{0} hits {1}.", MessageFormatter.Format("{0} hits {1}.", "Shade"));
        }

        [Test]
        public void Format_TooManyArguments_StillFormats()
        {
            // Extra arguments are harmless in string.Format - only missing ones throw.
            Assert.AreEqual("Shade hits.", MessageFormatter.Format("{0} hits.", "Shade", "ignored"));
        }

        [Test]
        public void ToRichTextHex_RoundTripsExactByteColorsWithoutDrifting()
        {
            // The failure this guards against is off-by-one from float truncation:
            // 0xC0 stored as 192/255f can come back as 191.99998 and render #BF.
            Assert.AreEqual("FDC090", MessageFormatter.ToRichTextHex(new Color32(0xFD, 0xC0, 0x90, 0xFF)));
            Assert.AreEqual("FF5D61", MessageFormatter.ToRichTextHex(new Color32(0xFF, 0x5D, 0x61, 0xFF)));
        }

        [Test]
        public void ToRichTextHex_ClampsOutOfRangeChannels()
        {
            Assert.AreEqual("FF0000", MessageFormatter.ToRichTextHex(new Color(4f, -2f, 0f)));
        }

        [Test]
        public void Colorize_WrapsTextInATmpColorTag()
        {
            Assert.AreEqual("<color=#FDC090>Old Compass</color>",
                MessageFormatter.Colorize("Old Compass", new Color32(0xFD, 0xC0, 0x90, 0xFF)));
        }
    }
}
