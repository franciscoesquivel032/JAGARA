namespace Jagara.Runtime.Narrative
{
    /// <summary>
    /// What kind of thing a name substituted into a message refers to. Drives the
    /// per-name tint the log applies, so "Old Compass" reads as an item and
    /// "Leecher" as an enemy without the player having to parse the sentence.
    /// </summary>
    public enum NameRole
    {
        None = 0,
        Item = 1,
        Enemy = 2
    }
}
