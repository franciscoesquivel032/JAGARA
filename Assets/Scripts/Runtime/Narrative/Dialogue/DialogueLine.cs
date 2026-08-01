using System;
using UnityEngine;

namespace Jagara.Runtime.Narrative.Dialogue
{
    /// <summary>
    /// A single spoken line: who says it, and what they say. A struct rather than
    /// a separate asset because a line has no identity outside the conversation
    /// it belongs to.
    /// </summary>
    [Serializable]
    public struct DialogueLine
    {
        [SerializeField] private SpeakerSO speaker;
        [SerializeField, TextArea(2, 5)] private string text;

        public DialogueLine(SpeakerSO speaker, string text)
        {
            this.speaker = speaker;
            this.text = text;
        }

        public SpeakerSO Speaker => speaker;
        public string Text => text;
    }
}
