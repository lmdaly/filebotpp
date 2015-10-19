using FileBotPP.Interfaces;

namespace FileBotPP.Tree
{
    public struct BadNameUpdate
    {
        public IDirectoryItem Directory { get; set; }
        public IFileItem File { get; set; }
        public string SuggestName { get; set; }
    }
}