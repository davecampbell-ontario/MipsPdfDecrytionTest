namespace MipsTestApp.Models.Protection.File.Content
{
    public static class FileHeaders
    {
        private static readonly byte[] BMP = [66, 77];
        private static readonly byte[] PDF_Bytes = [37, 80, 68, 70, 45]; // version is not checked
                                                                         // private static readonly byte[] PDF_Bytes_And_Version = [37, 80, 68, 70, 45, 49, 46]; // version is not checked
        private static readonly byte[] ZIP_DOCX_Bytes = [80, 75, 3, 4];
        private static readonly byte[] JPG_Bytes = [255, 216, 255];
        private static readonly byte[] PNG_Bytes = [137, 80, 78, 71, 13, 10, 26, 10];
        private static readonly byte[] GIF_Bytes = [71, 73, 70, 56];
        private static readonly byte[] WEBP1_Bytes = [82, 73, 70, 70]; // first four: R I F F
        private static readonly byte[] WEBP3_Bytes = [87, 69, 66, 80]; // third four: W E B P
        private static readonly byte[] SVG_Bytes = [60, 63, 120, 109, 108]; // <?xml

        public static readonly IReadOnlyDictionary<string, List<FileHeader>> Mappings = new Dictionary<string, List<FileHeader>>(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = [new() { MimeType = "application/pdf", HeaderBytes = PDF_Bytes, HeaderOffset = 0 }],
            [".zip"] = [new() { MimeType = "application/zip", HeaderBytes = ZIP_DOCX_Bytes, HeaderOffset = 0 }],
            [".docx"] = [new() { MimeType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document", HeaderBytes = [80, 75, 3, 4], HeaderOffset = 0 }],
            [".jpg"] = [new() { MimeType = "image/jpeg", HeaderBytes = JPG_Bytes, HeaderOffset = 0 }],
            [".png"] = [new() { MimeType = "image/png", HeaderBytes = PNG_Bytes, HeaderOffset = 0 }],
            [".gif"] = [new() { MimeType = "image/gif", HeaderBytes = GIF_Bytes, HeaderOffset = 0 }],
            [".bmp"] = [new() { MimeType = "image/bmp", HeaderBytes = BMP, HeaderOffset = 0 }],
            [".webp"] =
            [
                new() { MimeType = "image/webp", HeaderBytes = WEBP1_Bytes, HeaderOffset = 0 }, // RIFF
                new() { MimeType = "image/webp", HeaderBytes = WEBP3_Bytes, HeaderOffset = 8 } // WEBP
            ],
            [".svg"] = [new FileHeader { MimeType = "image/svg+xml", HeaderBytes = SVG_Bytes, HeaderOffset = 0 }]
        };
    }
}
