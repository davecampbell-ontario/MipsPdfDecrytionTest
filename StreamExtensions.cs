

namespace MipsTestApp.Extensions
{
    public static class StreamExtensions
    {
        public static async Task<byte[]> ReadAllBytesAsync(this Stream stream)
        {
            switch (stream)
            {
                case MemoryStream mem:
                    return mem.ToArray();
                default:
                    using (var m = new MemoryStream())
                    {
                        long originalPosition = 0;
                        if (stream.CanSeek)
                        {
                            originalPosition = stream.Position;
                            stream.Position = 0;
                        }

                        await stream.CopyToAsync(m);
                        if (stream.CanSeek)
                        {
                            stream.Position = originalPosition;
                        }
                        return m.ToArray();
                    }
            }
        }

        public static byte[] ReadAllBytes(this Stream stream)
        {
            switch (stream)
            {
                case MemoryStream mem:
                    return mem.ToArray();
                default:
                    using (var m = new MemoryStream())
                    {
                        long originalPosition = 0;
                        if (stream.CanSeek)
                        {
                            originalPosition = stream.Position;
                            stream.Position = 0;
                        }

                        stream.CopyTo(m);
                        if (stream.CanSeek)
                        {
                            stream.Position = originalPosition;
                        }
                        return m.ToArray();
                    }
            }
        }
    }
}
