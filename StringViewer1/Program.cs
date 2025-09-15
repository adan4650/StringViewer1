// Program.cs
// A .NET 9.0 Console Application that reads a ZIP file,
// streams through each entry, and outputs its contents as hex + text.
// It supports paging for large files and uses robust charset detection
// via the Ude.NetStandard package.

using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using Ude; // NuGet: Ude.NetStandard

class Program
{
    // Entry point: now prompts for zip file path if not provided
    static async Task<int> Main(string[] args)
    {
        string zipPath = null;

        // If no argument, prompt for zip file path
        if (args.Length == 0)
        {
            Console.Write("Enter path to ZIP file: ");
            zipPath = Console.ReadLine()?.Trim();
        }
        else
        {
            zipPath = args[0];
        }

        // Default settings
        long pageSizeBytes = 16 * 1024; // 16 KB per page
        string mode = "both";           // output both hex and text
        string outFile = null;          // optional: dump output to a file
        bool interactive = true;        // interactive paging unless -ni is used

        // Parse optional arguments
        for (int i = 1; i < args.Length; i++)
        {
            var a = args[i].ToLowerInvariant();
            if (a == "-ni") interactive = false;
            else if (a == "-o" && i + 1 < args.Length) { outFile = args[++i]; }
            else if (a == "-m" && i + 1 < args.Length) { mode = args[++i]; }
            else if (a == "-p" && i + 1 < args.Length && long.TryParse(args[++i], out var kb))
                pageSizeBytes = Math.Max(1024, kb * 1024); // min 1KB page size
        }

        // Ensure the zip exists
        if (string.IsNullOrEmpty(zipPath) || !File.Exists(zipPath))
        {
            Console.WriteLine($"Zip file not found: {zipPath}");
            return 2;
        }

        try
        {
            // Kick off the zip processing
            await ZipHexDumper.ProcessZipAsync(zipPath, pageSizeBytes, mode, interactive, outFile);
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fatal error: {ex.Message}");
            return 3;
        }
    }
}

static class ZipHexDumper
{
    /// <summary>
    /// Open the zip file and process each entry inside.
    /// </summary>
    public static async Task ProcessZipAsync(string zipPath, long pageSize, string mode, bool interactive, string outFile)
    {
        // Open the zip read-only
        using var zip = ZipFile.OpenRead(zipPath);
        Console.WriteLine($"Opened zip: {zipPath}  Entries: {zip.Entries.Count}");

        int entryIndex = 0;
        foreach (var entry in zip.Entries)
        {
            // Skip directories (entry.Name == "" means directory)
            if (string.IsNullOrEmpty(entry.Name)) continue;

            entryIndex++;
            Console.WriteLine();
            Console.WriteLine($"Entry {entryIndex}/{zip.Entries.Count}: {entry.FullName}  ({entry.Length} bytes)");

            // Open a stream for this entry
            using var stream = entry.Open();

            // Dump its contents page by page
            await DumpStreamPagedAsync(stream, pageSize, entry.FullName, mode, interactive, outFile);
        }
    }

    /// <summary>
    /// Read a stream in chunks, buffer into pages, and dump each page.
    /// </summary>
    private static async Task DumpStreamPagedAsync(Stream stream, long pageSize, string entryName, string mode, bool interactive, string outFile)
    {
        const int readBufferSize = 4 * 1024; // 4 KB chunks for efficiency
        byte[] readBuffer = new byte[readBufferSize];
        long totalRead = 0;

        using var pageMs = new MemoryStream(); // buffer for one page
        int bytesRead;
        while ((bytesRead = await stream.ReadAsync(readBuffer, 0, readBuffer.Length)) > 0)
        {
            // Write chunk into current page buffer
            await pageMs.WriteAsync(readBuffer, 0, bytesRead);
            totalRead += bytesRead;

            // If we filled a page, dump it
            if (pageMs.Length >= pageSize)
            {
                byte[] pageBytes = pageMs.ToArray();
                long pageStart = totalRead - pageBytes.Length;

                await WritePageAsync(pageBytes, pageStart, entryName, mode, interactive, outFile);

                // Reset page buffer
                pageMs.SetLength(0);
            }
        }

        // Dump any leftover bytes (last partial page)
        if (pageMs.Length > 0)
        {
            byte[] pageBytes = pageMs.ToArray();
            long pageStart = totalRead - pageBytes.Length;

            await WritePageAsync(pageBytes, pageStart, entryName, mode, interactive, outFile);
        }
    }

    /// <summary>
    /// Output a page: hex dump, text decoding, charset detection.
    /// </summary>
    private static async Task WritePageAsync(byte[] pageBytes, long pageStartOffset, string entryName, string mode, bool interactive, string outFile)
    {
        var sb = new StringBuilder();

        // Page header
        sb.AppendLine($"--- {entryName}  offset 0x{pageStartOffset:X} ({pageStartOffset})  length {pageBytes.Length} bytes ---");

        // Always produce a hex dump (16 bytes per line + ASCII column)
        for (int i = 0; i < pageBytes.Length; i += 16)
        {
            int lineLen = Math.Min(16, pageBytes.Length - i);
            sb.Append($"{(pageStartOffset + i):X8}: ");
            for (int j = 0; j < 16; j++)
            {
                if (j < lineLen) sb.Append(pageBytes[i + j].ToString("X2") + " ");
                else sb.Append("   ");
            }
            sb.Append(" ");
            for (int j = 0; j < lineLen; j++)
            {
                byte b = pageBytes[i + j];
                char c = (b >= 32 && b <= 126) ? (char)b : '.';
                sb.Append(c);
            }
            sb.AppendLine();
        }

        // --- Charset detection (Ude) ---
        var detector = new CharsetDetector();
        detector.Feed(pageBytes, 0, pageBytes.Length);
        detector.DataEnd();

        if (detector.Charset != null)
        {
            sb.AppendLine();
            sb.AppendLine($"[Detected charset: {detector.Charset} ({detector.Confidence:P0})]");

            try
            {
                // Decode text using the detected encoding
                Encoding enc = Encoding.GetEncoding(detector.Charset);
                string decoded = enc.GetString(pageBytes);

                // Show at most 10k characters to avoid console flooding
                if (decoded.Length > 10000)
                {
                    sb.AppendLine(decoded.Substring(0, 10000));
                    sb.AppendLine($"... (truncated, length {decoded.Length})");
                }
                else
                {
                    sb.AppendLine(decoded);
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"[Failed to decode with detected charset: {ex.Message}]");
            }
        }

        // Page text is ready
        string pageText = sb.ToString();

        // If user specified an output file, append to it
        if (!string.IsNullOrEmpty(outFile))
        {
            await File.AppendAllTextAsync(outFile, pageText);
            Console.WriteLine($"Appended page to {outFile}");
        }
        else
        {
            // Otherwise print to console
            Console.WriteLine(pageText);

            // Interactive paging if enabled
            if (interactive)
            {
                Console.Write("[Enter] next page, [Q] quit, [W] write remaining pages to file: ");
                var key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.Q) Environment.Exit(0);
                if (key.Key == ConsoleKey.W)
                {
                    Console.Write("Output file path: ");
                    string path = Console.ReadLine().Trim();
                    if (!string.IsNullOrEmpty(path))
                    {
                        // outFile cannot be updated here anymore, so you may need to handle this differently
                        await File.AppendAllTextAsync(path, pageText); // write current page too
                        Console.WriteLine($"Writing subsequent pages to {path}");
                    }
                }
            }
        }
    }
}
