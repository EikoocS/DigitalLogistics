using System;
using System.IO;

namespace DigitalLogistics.Tracing
{
    internal sealed class CountingWriteStream : Stream
    {
        private readonly Stream destination;
        internal long BytesWritten { get; private set; }

        internal CountingWriteStream(Stream destination) => this.destination = destination;

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => destination.CanWrite;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => destination.Flush();

        public override void Write(byte[] buffer, int offset, int count)
        {
            destination.Write(buffer, offset, count);
            BytesWritten += count;
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            destination.Write(buffer);
            BytesWritten += buffer.Length;
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
