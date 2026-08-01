namespace Jagara.Runtime.Narrative
{
    /// <summary>
    /// What kind of event a message describes. Purely presentational for now -
    /// TextBoxController maps categories to colors - but it also gives a future
    /// backlog viewer something to filter on without re-parsing message text.
    /// </summary>
    public enum MessageCategory
    {
        System = 0,
        Item = 1,
        Combat = 2,
        Floor = 3
    }
}
