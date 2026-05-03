namespace BlazorClaw.Core.Utils
{
    public static class StreamExtensions
    {

        public static byte[] ReadToBytes(this Stream stream) => ReadToBytes(stream, false);

        public static byte[] ReadToBytes(this Stream stream, bool closeStream)
        {
            if (stream == null) return new byte[0];
            try
            {

                if (stream is MemoryStream ms) return ms.ToArray();

                long originalPosition = 0;

                if (stream.CanSeek)
                {
                    originalPosition = stream.Position;
                    stream.Seek(0, SeekOrigin.Begin);
                }
                try
                {
                    using var mems = new MemoryStream();
                    stream.CopyTo(mems);
                    return mems.ToArray();
                }
                finally
                {
                    if (!closeStream && stream.CanSeek)
                    {
                        stream.Seek(originalPosition, SeekOrigin.Begin);
                    }
                }
            }
            finally
            {
                if (closeStream) stream.Dispose();
            }
        }

        public static Task<byte[]> ReadToBytesAsync(this Stream stream) => ReadToBytesAsync(stream, false);

        public static async Task<byte[]> ReadToBytesAsync(this Stream stream, bool closeStream)
        {
            if (stream == null) return [];
            try
            {
                if (stream is MemoryStream ms) return ms.ToArray();

                long originalPosition = 0;

                if (stream.CanSeek)
                {
                    originalPosition = stream.Position;
                    stream.Seek(0, SeekOrigin.Begin);
                }
                try
                {
                    using var mems = new MemoryStream();
                    await stream.CopyToAsync(mems);
                    return mems.ToArray();
                }
                finally
                {
                    if (!closeStream && stream.CanSeek)
                    {
                        stream.Seek(originalPosition, SeekOrigin.Begin);
                    }
                }
            }
            finally
            {
                if (closeStream) stream.Dispose();
            }
        }
    }
}
