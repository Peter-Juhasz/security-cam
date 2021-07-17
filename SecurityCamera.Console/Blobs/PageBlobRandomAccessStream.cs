using Azure;
using Azure.Storage;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;

using System;
using System.Buffers;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;

using Windows.Foundation;
using Windows.Storage.Streams;

namespace SecurityCamera.Console
{
    internal class PageBlobRandomAccessStream : IRandomAccessStream, IAsyncDisposable
    {
        public PageBlobRandomAccessStream(PageBlobClient client, BlobsOptions options)
        {
            _outputStream = new OutputStream(client, options);
        }

        private OutputStream _outputStream;

        public bool CanWrite => true;

        public ulong Position => (ulong)_outputStream.Position;

        public ulong Size
        {
            get => (ulong)_outputStream.Size;
            set
            {
            }
        }

        public IOutputStream GetOutputStreamAt(ulong position)
        {
            _outputStream.EnqueueSeek((long)position);
            return _outputStream;
        }

        public void Seek(ulong position) => _outputStream.EnqueueSeek((long)position);

        public IAsyncOperationWithProgress<uint, uint> WriteAsync(IBuffer buffer) => _outputStream.WriteAsync(buffer);

        public IAsyncOperation<bool> FlushAsync() => _outputStream.FlushAsync();


        #region Read
        public bool CanRead => false;

        public IInputStream GetInputStreamAt(ulong position) => throw new NotSupportedException();

        public IAsyncOperationWithProgress<IBuffer, uint> ReadAsync(IBuffer buffer, uint count, InputStreamOptions options) => throw new NotSupportedException();

        public IRandomAccessStream CloneStream() => throw new NotSupportedException();
        #endregion

        public void Dispose() => throw new InvalidOperationException();

        public ValueTask DisposeAsync() => _outputStream.DisposeAsync();

        class OutputStream : IOutputStream, IAsyncDisposable
        {
            public OutputStream(PageBlobClient client, BlobsOptions options)
            {
                Client = client;
                Options = options;
            }

            public PageBlobClient Client { get; }
            private BlobsOptions Options { get; }

            private Stream? _blobStream = null;
            private long _position = 0;
            private long _totalWritten = 0;
            private long _blobSize;
            private long _desiredPosition = 0;
            private bool _seekRequested = false;

            private const long PageSizeInBytes = 512;
            private byte[] _paddingBuffer = new byte[PageSizeInBytes];
            private byte[] _prependBuffer = new byte[PageSizeInBytes];

            public long Position => _position;
            public long Size => _totalWritten;

            private readonly byte[] _buffer = new byte[1024 * 1024];

            public IAsyncOperationWithProgress<uint, uint> WriteAsync(IBuffer buffer) => AsyncInfo.Run<uint, uint>(async (c, p) =>
            {
                // first write
                if (_blobStream == null)
                {
                    // initialize blob
                    _blobStream = await Client.OpenWriteAsync(overwrite: false, 0, new()
                    {
                        Size = Options.InitialSizeHint,
                        BufferSize = Options.BufferSize,
                    });

                    // keep track of blob size for resize
                    _blobSize = Options.InitialSizeHint;
                }

                // out of bounds
                if (_seekRequested)
                {
                    // flush and dispose currently active stream
                    await FlushAsync(c);
                    await using (_blobStream) ;

                    // open blob at specific page
                    await OpenAtPositionAsync(_desiredPosition, c);
                    _seekRequested = false;
                }

                // check for size constraints
                if (_totalWritten + buffer.Length > _blobSize)
                {
                    // resize
                    var newSize = (int)(_blobSize * Options.ResizeFactor);
                    await Client.ResizeAsync(newSize, cancellationToken: c);
                    _blobSize = newSize;
                    await OpenAtPositionAsync(_position, c);
                }

                // copy content buffered
                int bufferSize = _buffer.Length;
                int processed = 0;
                int remaining = (int)buffer.Length;
                do
                {
                    int amount = Math.Min(bufferSize, remaining);
                    buffer.CopyTo((uint)processed, _buffer, 0, amount);
                    await _blobStream.WriteAsync(_buffer, 0, amount, c);
                    processed += amount;
                    remaining -= amount;
                } while (remaining > 0);

                // advance positions
                Advance(buffer.Length);

                return buffer.Length;
            });

            private async Task OpenAtPositionAsync(long desiredPosition, CancellationToken c)
            {
                var pageStartOffset = desiredPosition - desiredPosition % PageSizeInBytes;
                _blobStream = await Client.OpenWriteAsync(overwrite: false, pageStartOffset, new()
                {
                    BufferSize = Options.BufferSize,
                });
                if (pageStartOffset != desiredPosition)
                {
                    // data prepending actual position must be loaded from page to buffer
                    var offsetLength = (int)(desiredPosition % PageSizeInBytes);
                    var response = await Client.DownloadStreamingAsync(new HttpRange(pageStartOffset, offsetLength), cancellationToken: c);
                    using var prepend = response.Value.Content;
                    var read = await prepend.ReadAsync(_prependBuffer, 0, offsetLength, c);
                    _blobStream.Write(_prependBuffer, 0, read);
                }

                _position = desiredPosition;
            }

            private async Task EnsurePagePaddingAsync(CancellationToken c)
            {
                // no need to pad
                if (_position % PageSizeInBytes == 0)
                {
                    return;
                }

                var remainingLength = (int)(PageSizeInBytes - _position % PageSizeInBytes);
                
                // end of the stream,
                // pad with empty bytes
                if (_position == _totalWritten)
                {
                    _blobStream!.Write(_paddingBuffer, 0, remainingLength);
                    _position += remainingLength;
                }

                // in the middle of the stream,
                // so we need to look up content which has already been persisted
                else
                {
                    var response = await Client.DownloadStreamingAsync(new HttpRange(_position, remainingLength), cancellationToken: c);
                    using var prepend = response.Value.Content;
                    var read = await prepend.ReadAsync(_prependBuffer, 0, remainingLength, c);
                    _blobStream!.Write(_prependBuffer, 0, remainingLength);
                }
            }

            public IAsyncOperation<bool> FlushAsync() => AsyncInfo.Run<bool>(async c =>
            {
                await FlushAsync(c);
                return true;
            });

            private async Task FlushAsync(CancellationToken c)
            {
                await EnsurePagePaddingAsync(c);
                if (_blobStream is Stream stream)
                {
                    await stream.FlushAsync(c);
                }
            }

            private void Advance(long length)
            {
                _position += length;

                // keep track of how the actual size of data written
                if (_position > _totalWritten)
                {
                    _totalWritten = _position;
                }
            }

            public void EnqueueSeek(long desiredPosition)
            {
                // doesn't have to seek
                if (desiredPosition == _position)
                {
                    return;
                }

                // enqueue seek
                _desiredPosition = desiredPosition;
                _seekRequested = true;
            }

            public void Dispose() { }

            public async ValueTask DisposeAsync()
            {
                if (_blobStream != null)
                {
                    // flush data and dispose resources
                    await FlushAsync(default);
                    await using (_blobStream) ;

                    // right-size page blob to free up space
                    var sizePageOffset = Size % PageSizeInBytes;
                    var padding = sizePageOffset == 0 ? 0 : PageSizeInBytes - sizePageOffset;
                    var optimalSize = Size + padding;
                    if (_blobSize != optimalSize)
                    {
                        await Client.ResizeAsync(optimalSize);
                    }
                }
            }
        }
    }
}
