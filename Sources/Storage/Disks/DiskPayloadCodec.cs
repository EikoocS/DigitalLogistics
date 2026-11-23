using System;
using System.IO;
using System.IO.Compression;
using DigitalLogistics.Tracing;

namespace DigitalLogistics.Storage.Disks
{
    internal static class DiskPayloadCodec
    {
        internal static string Encode(Action<TextWriter> write, bool measure, out DiskPayloadSize size)
        {
            using var buffer = new MemoryStream();
            CountingWriteStream counter = null;
            using (var compression = new GZipStream(buffer, CompressionMode.Compress, leaveOpen: true))
            {
                if (measure) counter = new CountingWriteStream(compression);
                using var text = new StreamWriter(counter ?? (Stream)compression);
                write(text);
            }
            string encoded = Convert.ToBase64String(buffer.GetBuffer(), 0, (int)buffer.Length);
            size = counter == null ? default : new DiskPayloadSize(counter.BytesWritten, buffer.Length, encoded.Length);
            return encoded;
        }

        internal static T Decode<T>(string value, Func<TextReader, T> read)
        {
            using var buffer = new MemoryStream(Convert.FromBase64String(value));
            using var compression = new GZipStream(buffer, CompressionMode.Decompress);
            using var text = new StreamReader(compression);
            return read(text);
        }
    }
}
