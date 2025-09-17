using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading;
using System.Threading.Tasks;

namespace StringViewer1
{
    public class PageProvider : IDisposable
    {
        private readonly MemoryMappedFile _mmf;
        private readonly long _fileLength;
        private readonly int _pageSize;
        private readonly LruCache<long, byte[]> _cache;
        private readonly object _readLock = new();

        public PageProvider(string path, int pageSize = 64 * 1024, int cachePages = 128)
        {
            _pageSize = pageSize;
            var fi = new FileInfo(path);
            _fileLength = fi.Length;
            _mmf = MemoryMappedFile.CreateFromFile(path, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
            _cache = new LruCache<long, byte[]>(cachePages);
        }

        public long FileLength => _fileLength;
        public int PageSize => _pageSize;
        public long PageCount => (_fileLength + _pageSize - 1) / _pageSize;

        public Task<byte[]> GetPageAsync(long pageIndex, CancellationToken ct = default)
        {
            if (pageIndex < 0 || pageIndex >= PageCount) throw new ArgumentOutOfRangeException(nameof(pageIndex));

            if (_cache.TryGet(pageIndex, out var cached)) return Task.FromResult(cached);

            return Task.Run(() =>
            {
                long offset = pageIndex * (long)_pageSize;
                int bytesToRead = (int)Math.Min(_pageSize, _fileLength - offset);
                var buffer = new byte[bytesToRead];

                lock (_readLock)
                {
                    using var accessor = _mmf.CreateViewAccessor(offset, bytesToRead, MemoryMappedFileAccess.Read);
                    accessor.ReadArray(0, buffer, 0, bytesToRead);
                }

                _cache.Add(pageIndex, buffer);
                return buffer;
            }, ct);
        }

        public void Dispose() => _mmf.Dispose();
    }
}