using System.ComponentModel;
using System.Text;

namespace OLAF.Tools;

public sealed class FileSystemTools
{
    private const int MaxFileSizeBytes = 50_000;

    [Description("Read the contents of a source code file. Use this when the user asks you to review, explain, or analyze a specific file.")]
    public string ReadFile(
        [Description("The absolute or relative path to the file to read.")]
        string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            return $"[Error] File not found: {fullPath}";
        }

        var fileInfo = new FileInfo(fullPath);
        if (fileInfo.Length > MaxFileSizeBytes)
        {
            return $"[Error] File too large ({fileInfo.Length:N0} bytes). Max: {MaxFileSizeBytes:N0}";
        }

        return File.ReadAllText(fullPath);
    }

    [Description("List files in a directory matching an optional glob pattern. Useful for exploring project structure before reading specific files.")]
    public string ListFiles(
        [Description("The directory path to list. Use '.' for the current directory.")]
        string directory,
        [Description("Optional glob pattern, e.g. '*.cs', '*.json'. Leave empty for all files.")]
        string pattern = "*")
    {
        var fullPath = Path.GetFullPath(directory);
        if (!Directory.Exists(fullPath))
        {
            return $"[Error] Directory not found: {fullPath}";
        }

        var files = Directory.GetFiles(fullPath, pattern, SearchOption.TopDirectoryOnly);
        var directories = Directory.GetDirectories(fullPath);

        var sb = new StringBuilder();
        sb.AppendLine($"Contents of: {fullPath}");

        foreach (var dir in directories.OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
        {
            sb.AppendLine($"[DIR]  {Path.GetFileName(dir)}/");
        }

        foreach (var file in files.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            sb.AppendLine($"[FILE] {Path.GetFileName(file)}  ({new FileInfo(file).Length:N0} bytes)");
        }

        return sb.ToString();
    }

    [Description("Get the current working directory of the CLI tool.")]
    public string GetCurrentDirectory() => Directory.GetCurrentDirectory();
}