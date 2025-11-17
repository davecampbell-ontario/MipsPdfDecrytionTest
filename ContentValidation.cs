

namespace MipsTestApp.Models.Protection.File.Content
{
    /// <summary>
    /// This is a content-based MIME type validation
    /// </summary>
    public static class ContentValidation
    {
        private const int MaxHeaderSize = 16;

        /// <summary>
        /// Validates a file from an IFormFile content matches its extension.
        /// </summary>
        /// <param name="fileUpload">Wrapper for the uploaded file></param>
        /// <returns>True if the file is valid; otherwise, false.</returns>
        /// <remarks>
        /// This method checks the file's magic number (header bytes) against predefined mappings
        /// to ensure the file's content matches its extension. It relies on the <see cref="FileHeaders.Mappings"/>
        /// dictionary to retrieve the expected magic numbers for the given file extension.
        /// </remarks>
        public static bool IsMimeTypeContentMatching<T>(IFormFile fileUpload)
        {
            if (fileUpload.FileName is null || fileUpload.Length == 0)
            {
                return false;
            }
            string extension = Path.GetExtension(fileUpload.FileName);
            if (string.IsNullOrEmpty(extension) || !FileHeaders.Mappings.ContainsKey(extension))
            {
                return false;
            }
            using var stream = fileUpload.OpenReadStream();
            return CheckInternal(stream, extension);
        }

        /// <summary>
        /// Validates a byte array content matches its extension.
        /// </summary>
        /// <param name="fileBytes">The byte array containing the file's data.</param>
        /// <param name="fileExtension">The expected file extension (e.g., ".pdf").</param>
        /// <returns>True if the file is valid; otherwise, false.</returns>
        /// <remarks>
        /// This method checks the file's magic number (header bytes) against predefined mappings
        /// to ensure the file's content matches its extension. It relies on the <see cref="FileHeaders.Mappings"/>
        /// dictionary to retrieve the expected magic numbers for the given file extension.
        /// </remarks>
        public static bool IsMimeTypeContentMatching(byte[] fileBytes, string fileExtension)
        {
            if (fileBytes == null || fileBytes.Length == 0 || string.IsNullOrEmpty(fileExtension) || !FileHeaders.Mappings.ContainsKey(fileExtension))
            {
                return false;
            }

            using var stream = new MemoryStream(fileBytes);
            return CheckInternal(stream, fileExtension);
        }

        /// <summary>
        /// Validates whether the content of a given stream matches the expected file extension.
        /// </summary>
        /// <param name="stream">The stream containing the file's data.</param>
        /// <param name="fileExtension">The expected file extension (e.g., ".pdf").</param>
        /// <returns>True if the content matches the expected file extension; otherwise, false.</returns>
        /// <remarks>
        /// This method checks the file's magic number (header bytes) against predefined mappings
        /// to ensure the file's content matches its extension. It relies on the <see cref="FileHeaders.Mappings"/>
        /// dictionary to retrieve the expected magic numbers for the given file extension.
        /// </remarks>
        public static bool IsMimeTypeContentMatching(Stream stream, string fileExtension)
        {
            return stream is not null
                    && !string.IsNullOrEmpty(fileExtension)
                    && FileHeaders.Mappings.ContainsKey(fileExtension) && CheckInternal(stream, fileExtension);
        }

        /// <summary>
        /// Internal method to perform the magic number check from a stream.
        /// </summary>
        private static bool CheckInternal(Stream stream, string fileExtension)
        {
            byte[] header = new byte[MaxHeaderSize];
            stream.Read(header, 0, MaxHeaderSize);

            if (!FileHeaders.Mappings.TryGetValue(fileExtension, out var headers))
            {
                return false;
            }

            foreach (var fileHeader in headers)
            {
                if (CheckBytes(header, fileHeader.HeaderBytes, fileHeader.HeaderOffset))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool CheckBytes(byte[] fileHeader, byte[] magicNumber, int offset = 0)
        {
            if (fileHeader.Length < offset + magicNumber.Length)
            {
                return false;
            }

            for (int i = 0; i < magicNumber.Length; i++)
            {
                if (fileHeader[offset + i] != magicNumber[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}

