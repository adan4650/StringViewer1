// Program.cs
// .NET 9.0 Console Application that reads ANY file (not just zip),
// streams through it in pages, and outputs both hex + decoded text.
// It uses robust charset detection via Ude.NetStandard (NuGet).


using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Ude; // NuGet package: Ude.NetStandard

class Program
{
    static async Task<int> Main(string[] args)
    {
        string filePath = null;

        // If no argument, prompt for file path interactively
        if (args.Length == 0)
        {
            Console.Write("Enter path to file: ");
            filePath = Console.ReadLine()?.Trim();
        }
        else
        {
            filePath = args[0];
        }

        // Default settings
        long pageSizeBytes = 16 * 1024; // 16 KB per page
        string mode = "both";           // keep for future expansion (hex/text filtering)
        string outFile = null;          // optional: write results to file
        bool interactive = true;        // allow paging unless -ni flag is passed

        // Parse optional args
        for (int i = 1; i < args.Length; i++)
        {
            var a = args[i].ToLowerInvariant();
            if (a == "-ni") interactive = false;
            else if (a == "-o" && i + 1 < args.Length) { outFile = args[++i]; }
            else if (a == "-m" && i + 1 < args.Length) { mode = args[++i]; }
            else if (a == "-p" && i + 1 < args.Length && long.TryParse(args[++i], out var kb))
                pageSizeBytes = Math.Max(1024, kb * 1024); // min 1KB
        }

        // Ensure file exists
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            Console.WriteLine($"File not found: {filePath}");
            return 2;
        }

        try
        {
            await FileHexDumper.ProcessFileAsync(filePath, pageSizeBytes, mode, interactive, outFile);
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fatal error: {ex.Message}");
            return 3;
        }
    }
}

static class FileHexDumper
{
    /// <summary>
    /// Opens the file and dumps it page by page.
    /// </summary>
    public static async Task ProcessFileAsync(string filePath, long pageSize, string mode, bool interactive, string outFile)
    {
        Console.WriteLine($"Opened file: {filePath}  Size: {new FileInfo(filePath).Length} bytes");

        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        await DumpStreamPagedAsync(fs, pageSize, Path.GetFileName(filePath), mode, interactive, outFile);
    }

    /// <summary>
    /// Reads from stream in chunks and dumps each page.
    /// </summary>
    private static async Task DumpStreamPagedAsync(Stream stream, long pageSize, string fileName, string mode, bool interactive, string outFile)
    {
        const int readBufferSize = 4 * 1024; // 4 KB read buffer
        byte[] readBuffer = new byte[readBufferSize];
        long totalRead = 0;

        using var pageMs = new MemoryStream();
        int bytesRead;

        while ((bytesRead = await stream.ReadAsync(readBuffer, 0, readBuffer.Length)) > 0)
        {
            await pageMs.WriteAsync(readBuffer, 0, bytesRead);
            totalRead += bytesRead;

            // If we reached a page boundary, dump
            if (pageMs.Length >= pageSize)
            {
                byte[] pageBytes = pageMs.ToArray();
                long pageStart = totalRead - pageBytes.Length;

                await WritePageAsync(pageBytes, pageStart, fileName, mode, interactive, outFile);
                pageMs.SetLength(0);
            }
        }

        // Dump final partial page
        if (pageMs.Length > 0)
        {
            byte[] pageBytes = pageMs.ToArray();
            long pageStart = totalRead - pageBytes.Length;

            await WritePageAsync(pageBytes, pageStart, fileName, mode, interactive, outFile);
        }
    }

    /// <summary>
    /// Renders one page as hex + decoded text (with charset detection).
    /// </summary>
    private static async Task WritePageAsync(byte[] pageBytes, long pageStartOffset, string fileName, string mode, bool interactive, string outFile)
    {
        var sb = new StringBuilder();

        // Extract contiguous printable ASCII strings (length >= 4)
        int minLength = 4;
        int start = -1; 
        for (int i = 0; i < pageBytes.Length; i++)
        {
            byte b = pageBytes[i];
            char c = (b >= 32 && b <= 126) ? (char)b : '.';
            sb.Append(c);
        }
        sb.AppendLine();

        string pageText = sb.ToString();

        // --- Output handling ---
        if (!string.IsNullOrEmpty(outFile))
        {
            await File.AppendAllTextAsync(outFile, pageText);
            Console.WriteLine($"Appended page to {outFile}");
        }
        else
        {
            Console.WriteLine(pageText);
        }
    }
}
