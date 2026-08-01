namespace Jagara.Runtime.Narrative
{
    /// <summary>
    /// One already-formatted line in the message log. Immutable: a message is
    /// built once (from a MessageTemplateSO plus its arguments) and then only
    /// read, so the log's ring buffer can hand copies out freely.
    /// </summary>
    public readonly struct GameMessage
    {
        public readonly string Text;
        public readonly MessageCategory Category;

        public GameMessage(string text, MessageCategory category)
        {
            Text = text;
            Category = category;
        }
    }
}
