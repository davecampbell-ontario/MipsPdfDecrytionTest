namespace MipsTestApp.Services.Protection
{
    public enum InvalidReason
    {
        UnKnown = 0,
        FileType = 1,
        ProtectionType = 2,
        SensitivityLevel = 3,
        AccessDenied = 4,
        NotEditor = 5,
        NotExtractor = 6,
        AlreadyUnprotected = 7,
        ProtectionIsNotSupported = 8,

        //More for Decrypted file checks (post decryption/unprotect)
        ContentType = 9
    }
}
