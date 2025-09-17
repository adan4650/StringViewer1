using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Ude;

namespace StringViewer1
{
    public class PageViewModel : INotifyPropertyChanged
    {
        private readonly PageProvider _provider;
        private readonly long _pageIndex;
        private readonly CancellationTokenSource _cts = new();

        public long PageIndex => _pageIndex;
        public long Offset => _pageIndex * (long)_provider.PageSize;
        public string? HexDump { get; private set; }
        public string? TextPreview { get; private set; }
        public string DisplayHeader => $"Page {_pageIndex} (offset 0x{Offset:X})";

        public event PropertyChangedEventHandler? PropertyChanged;

        public PageViewModel(PageProvider provider, long pageIndex)
        {
            _provider = provider;
            _pageIndex = pageIndex;
        }

        public async void EnsureLoadedAsync()
        {
            try
            {
                var bytes = await _provider.GetPageAsync(_pageIndex, _cts.Token).ConfigureAwait(false);
                HexDump = BuildHexDump(bytes, Offset);
                TextPreview = TryDetectAndDecode(bytes);
                Notify(nameof(HexDump));
                Notify(nameof(TextPreview));
            }
            catch (Exception ex)
            {
                TextPreview = $"[Error reading page: {ex.Message}]";
                Notify(nameof(TextPreview));
            }
        }

        private static string BuildHexDump(byte[] bytes, long offset)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < bytes.Length; i += 16)
            {
                int lineLen = Math.Min(16, bytes.Length - i);
                sb.Append($"{(offset + i):X8}: ");
                for (int j = 0; j < 16; j++)
                {
                    if (j < lineLen) sb.Append(bytes[i + j].ToString("X2") + " ");
                    else sb.Append("   ");
                }
                sb.Append(" ");
                for (int j = 0; j < lineLen; j++)
                {
                    byte b = bytes[i + j];
                    char c = (b >= 32 && b <= 126) ? (char)b : '.';
                    sb.Append(c);
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }

        private static string TryDetectAndDecode(byte[] bytes)
        {
            var detector = new CharsetDetector();
            detector.Feed(bytes, 0, bytes.Length);
            detector.DataEnd();

            if (!string.IsNullOrEmpty(detector.Charset))
            {
                try
                {
                    var enc = Encoding.GetEncoding(detector.Charset);
                    var decoded = enc.GetString(bytes);
                    return $"[Detected: {detector.Charset}]\n{decoded.Substring(0, Math.Min(2000, decoded.Length))}";
                }
                catch { }
            }

            return Encoding.ASCII.GetString(bytes);
        }

        private void Notify([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}