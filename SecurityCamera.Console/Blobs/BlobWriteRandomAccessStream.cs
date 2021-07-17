using System;
using System.IO;

using Windows.Foundation;
using Windows.Storage.Streams;

namespace SecurityCamera.Console
{
    internal class BlobWriteRandomAccessStream : IRandomAccessStream
    {
        public BlobWriteRandomAccessStream(Stream stream)
        {
            Stream = stream;
        }

        public Stream Stream { get; }

        public bool CanRead => false;

        public bool CanWrite => true;

        public ulong Position => (ulong)Stream.Position;

        public ulong Size { get => (ulong)Stream.Length; set => throw new NotSupportedException(); }

        public IRandomAccessStream CloneStream() => throw new NotSupportedException();

        public void Dispose() => Stream.Dispose();

        public IAsyncOperation<bool> FlushAsync() => GetOutputStreamAt(0).FlushAsync();

        public IInputStream GetInputStreamAt(ulong position) => throw new NotSupportedException();

        public IOutputStream GetOutputStreamAt(ulong position) => position switch 
        {
            0 => Stream.AsOutputStream(),
            _ => throw new NotSupportedException()
        };

        public IAsyncOperationWithProgress<IBuffer, uint> ReadAsync(IBuffer buffer, uint count, InputStreamOptions options) => throw new NotSupportedException();

        public void Seek(ulong position) => throw new NotSupportedException();

        public IAsyncOperationWithProgress<uint, uint> WriteAsync(IBuffer buffer) => throw new NotSupportedException();
    }
}
