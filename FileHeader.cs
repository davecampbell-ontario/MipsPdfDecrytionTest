namespace MipsTestApp.Models.Protection.File.Content
{
    public sealed class FileHeader
    {
        public required string MimeType { get; init; }
        public required byte[] HeaderBytes { get; init; }
        public required int HeaderOffset { get; init; }
    }
}
