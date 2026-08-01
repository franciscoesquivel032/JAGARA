using UnityEngine;

namespace Jagara.Runtime.Narrative
{
    /// <summary>
    /// One authorable log line, e.g. "{0} takes {1} damage." Kept as an asset per
    /// message (rather than a bank of string fields on one SO) so wording can be
    /// rewritten in the Inspector without recompiling, and so each emitter names
    /// the exact message it posts via a [SerializeField] reference - no string
    /// keys, no Resources.Load.
    /// </summary>
    [CreateAssetMenu(fileName = "New Message Template", menuName = "Jagara/Message Template")]
    public class MessageTemplateSO : ScriptableObject
    {
        [Tooltip("Message text with {0}, {1}... placeholders for the arguments the caller passes.")]
        [SerializeField, TextArea] private string template;

        [SerializeField] private MessageCategory category = MessageCategory.System;

        public string Template => template;
        public MessageCategory Category => category;

        public GameMessage Build(params object[] args) =>
            new GameMessage(MessageFormatter.Format(template, args), category);
    }
}
