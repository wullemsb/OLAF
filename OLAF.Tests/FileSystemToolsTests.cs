using OLAF.Tools;

namespace OLAF.Tests;

public sealed class FileSystemToolsTests : IDisposable
{
    private readonly FileSystemTools _sut = new();
    private readonly string _tempDir;

    public FileSystemToolsTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"OLAFTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void ReadFile_ReturnsContents_WhenFileExists()
    {
        var path = Path.Combine(_tempDir, "test.txt");
        File.WriteAllText(path, "hello world");

        var result = _sut.ReadFile(path);

        Assert.Equal("hello world", result);
    }

    [Fact]
    public void ReadFile_ReturnsError_WhenFileDoesNotExist()
    {
        var path = Path.Combine(_tempDir, "nonexistent.txt");

        var result = _sut.ReadFile(path);

        Assert.StartsWith("[Error] File not found:", result);
    }

    [Fact]
    public void ReadFile_ReturnsError_WhenFileTooLarge()
    {
        var path = Path.Combine(_tempDir, "large.bin");
        File.WriteAllBytes(path, new byte[51_000]);

        var result = _sut.ReadFile(path);

        Assert.StartsWith("[Error] File too large", result);
    }

    [Fact]
    public void ReadFile_AcceptsRelativePath()
    {
        var fileName = "relative.txt";
        var path = Path.Combine(Directory.GetCurrentDirectory(), fileName);
        File.WriteAllText(path, "relative content");

        try
        {
            var result = _sut.ReadFile(fileName);
            Assert.Equal("relative content", result);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ListFiles_ReturnsContents_WhenDirectoryExists()
    {
        File.WriteAllText(Path.Combine(_tempDir, "a.cs"), "");
        File.WriteAllText(Path.Combine(_tempDir, "b.cs"), "");

        var result = _sut.ListFiles(_tempDir);

        Assert.Contains("a.cs", result);
        Assert.Contains("b.cs", result);
    }

    [Fact]
    public void ListFiles_ReturnsError_WhenDirectoryDoesNotExist()
    {
        var path = Path.Combine(_tempDir, "nonexistent");

        var result = _sut.ListFiles(path);

        Assert.StartsWith("[Error] Directory not found:", result);
    }

    [Fact]
    public void ListFiles_WithPattern_FiltersResults()
    {
        File.WriteAllText(Path.Combine(_tempDir, "file.cs"), "");
        File.WriteAllText(Path.Combine(_tempDir, "file.json"), "");

        var result = _sut.ListFiles(_tempDir, "*.cs");

        Assert.Contains("file.cs", result);
        Assert.DoesNotContain("file.json", result);
    }

    [Fact]
    public void ListFiles_ListsSubdirectories()
    {
        var subDir = Path.Combine(_tempDir, "subdir");
        Directory.CreateDirectory(subDir);

        var result = _sut.ListFiles(_tempDir);

        Assert.Contains("[DIR]  subdir/", result);
    }

    [Fact]
    public void ListFiles_IncludesFileSizeInOutput()
    {
        var path = Path.Combine(_tempDir, "sized.txt");
        File.WriteAllText(path, "12345");

        var result = _sut.ListFiles(_tempDir);

        Assert.Contains("[FILE]", result);
        Assert.Contains("bytes", result);
    }

    [Fact]
    public void GetCurrentDirectory_ReturnsCurrentDirectory()
    {
        var result = _sut.GetCurrentDirectory();

        Assert.Equal(Directory.GetCurrentDirectory(), result);
    }

    [Fact]
    public void GetCurrentDirectory_ReturnsNonEmptyString()
    {
        var result = _sut.GetCurrentDirectory();

        Assert.False(string.IsNullOrWhiteSpace(result));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}
