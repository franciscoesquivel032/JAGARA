namespace Jagara.Runtime.UI.MenuActions
{
    /// <summary>
    /// A confirmable behavior attached to a menu option row. ActionMenuController
    /// looks this up per row and calls Execute() on confirm - it never needs to
    /// know which concrete behavior a given row has.
    /// </summary>
    public interface IMenuAction
    {
        string Description { get; }
        void Execute();
    }
}
