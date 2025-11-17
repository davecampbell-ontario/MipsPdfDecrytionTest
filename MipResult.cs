using Microsoft.InformationProtection;

namespace MipsTestApp.Services.Protection
{
    public record MipResult(string FileName, byte[] OriginalFileBytes, bool IsValid, bool IsEncypted, byte[] DecryptedFileBytes, ContentLabel ContentLabel, List<InvalidReason> InvalidReasons, MipRepublishResult RepublishResult = null);
}