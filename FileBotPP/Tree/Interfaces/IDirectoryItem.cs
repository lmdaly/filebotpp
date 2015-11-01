namespace FileBotPP.Tree.Interfaces
{
    public interface IDirectoryItem : IItem
    {
        IFileItem ContainsFile( string name );
        IDirectoryItem ContainsDirectory( string name );
        bool Polling { get; set; }
    }
}