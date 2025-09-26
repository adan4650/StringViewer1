📖 StringViewer1
Overview

StringViewer1 is a WPF application built on .NET 9.0 that can open and inspect very large files (2GB+).
It reads data in pages using MemoryMappedFile, displays it in a virtualized UI, and uses charset detection to show human-readable text when possible.

This makes it useful for:

Viewing binary files

Debugging encoded text files

Inspecting archives or logs that are too large for editors like Notepad++

✨ Features

Open large files (2GB+) safely without loading them fully into memory.

Hex + text preview per page with ASCII fallback.

Paging system (default 64KB per page).

LRU caching keeps only recently accessed pages in memory.

VirtualizingStackPanel ensures smooth scrolling in the UI.

Charset detection using Ude.NetStandard
.

Manage multiple files:

Add/remove files from a selection list.

Switch between files without reloading.

Save decoded content to text.

🚀 Getting Started
Prerequisites

Visual Studio 2022+
 with .NET Desktop Development workload.

.NET 9.0 SDK
.

Setup

Clone or download the repository.

Open the solution in Visual Studio.

Restore NuGet packages (Ude.NetStandard is required).

Press F5 to run.

Controls

Browse → Select a file to open.

Add Another File → Add more files to the session.

Remove File → Remove the selected file from the list.

Display File → Load and view selected file.

Save File → Export the decoded content to a .txt file.

⚡ Performance Design

MemoryMappedFile: Accesses large files efficiently by mapping pages.

Synchronous reads: Avoids Task.Run overhead for each page.

LRU Cache: Keeps recently accessed pages for smooth scrolling.

Charset detection once per file: Speeds up decoding by avoiding per-page detection.

Virtualizing UI: Only renders visible pages.

🛠 Project Structure

App.xaml / App.xaml.cs → Application entry point.

MainWindow.xaml / MainWindow.xaml.cs → UI + file management logic.

PageProvider.cs → Handles file paging via MemoryMappedFile.

PageViewModel.cs → Wraps a page with hex + text preview.

PagedFileCollection.cs → Virtualized collection for binding.

LruCache.cs → Simple Least Recently Used cache for pages.

📂 Example Workflow

Start the app.

Click Browse and pick a large file (binary, log, etc.).

The app will show:

Hex dump of file data.

Detected or ASCII-decoded text.

Charset info in the status bar.

Scroll through pages without memory issues.

Optionally, save the previewed text to a file.

📜 License

This project is for educational and personal use.
You may adapt it for your own needs.
