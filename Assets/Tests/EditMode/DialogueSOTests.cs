using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Jagara.Runtime.Narrative.Dialogue;

namespace Jagara.Tests.EditMode
{
    public class DialogueSOTests
    {
        [Test]
        public void Lines_ArePlayedBackInAuthoredOrder()
        {
            var voice = CreateSpeaker("The Voice");
            var dialogue = CreateDialogue(
                new DialogueLine(voice, "You should sleep now."),
                new DialogueLine(voice, "Shouldn't you?"));

            Assert.AreEqual(2, dialogue.LineCount);
            Assert.AreEqual("You should sleep now.", dialogue.Lines[0].Text);
            Assert.AreEqual("Shouldn't you?", dialogue.Lines[1].Text);
            Assert.AreSame(voice, dialogue.Lines[0].Speaker);

            Object.DestroyImmediate(dialogue);
            Object.DestroyImmediate(voice);
        }

        [Test]
        public void EmptyDialogue_ReportsZeroLinesWithoutThrowing()
        {
            var dialogue = ScriptableObject.CreateInstance<DialogueSO>();

            Assert.AreEqual(0, dialogue.LineCount);
            Assert.AreEqual(0, dialogue.Lines.Count);

            Object.DestroyImmediate(dialogue);
        }

        [Test]
        public void LineWithNoSpeaker_IsAllowed()
        {
            // Narration with no attributed speaker - the panel renders an empty
            // name rather than refusing to play the line.
            var dialogue = CreateDialogue(new DialogueLine(null, "The house exhales."));

            Assert.IsNull(dialogue.Lines[0].Speaker);
            Assert.AreEqual("The house exhales.", dialogue.Lines[0].Text);

            Object.DestroyImmediate(dialogue);
        }

        private static DialogueSO CreateDialogue(params DialogueLine[] lines)
        {
            var dialogue = ScriptableObject.CreateInstance<DialogueSO>();
            MessageTemplateSOTests.SetPrivateField(dialogue, "lines", new List<DialogueLine>(lines));
            return dialogue;
        }

        private static SpeakerSO CreateSpeaker(string displayName)
        {
            var speaker = ScriptableObject.CreateInstance<SpeakerSO>();
            MessageTemplateSOTests.SetPrivateField(speaker, "displayName", displayName);
            return speaker;
        }
    }
}
