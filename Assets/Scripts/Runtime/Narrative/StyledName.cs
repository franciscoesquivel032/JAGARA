namespace Jagara.Runtime.Narrative
{
    /// <summary>
    /// A name to substitute into a message, tagged with what it refers to so the
    /// log can tint it. Emitters build these instead of passing bare strings:
    /// <c>log.Post(pickedUpMessage, StyledName.Item(item.DisplayName))</c>.
    /// <para>
    /// The role, not the colour, travels with the value - callers say what a name
    /// <em>is</em> and the palette on MessageLogSO decides how it looks, so a
    /// retint never means touching gameplay code.
    /// </para>
    /// </summary>
    public readonly struct StyledName
    {
        public readonly string Text;
        public readonly NameRole Role;

        public StyledName(string text, NameRole role)
        {
            Text = text;
            Role = role;
        }

        public static StyledName Item(string text) => new StyledName(text, NameRole.Item);

        public static StyledName Enemy(string text) => new StyledName(text, NameRole.Enemy);

        /// <summary>
        /// Plain, untinted text. Only reached if a StyledName is handed straight to
        /// string.Format without going through the log's palette - the message is
        /// still correct, just uncoloured.
        /// </summary>
        public override string ToString() => Text;
    }
}
