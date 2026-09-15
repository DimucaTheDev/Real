namespace Real.Core.Assets.Providers;

public class DiskStorageProvider(string root) : StorageProvider
{
    public override bool IsReadOnly => false;
    
    public readonly string SystemRoot = Path.GetFullPath(root);

    private string GetFullPath(string path)
    {
        string combined = Path.Combine(SystemRoot, path);

        string fullPath = Path.GetFullPath(combined);
        if (!fullPath.StartsWith(SystemRoot, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException($"Attempt to access path outside of root: {path}");

        return fullPath;
    }

    public override bool FileExists(string path) => File.Exists(GetFullPath(path));

    public override bool DirectoryExists(string path) => Directory.Exists(GetFullPath(path));

    public override Stream OpenRead(string path) => File.OpenRead(GetFullPath(path));

    public override byte[] ReadAllBytes(string path) => File.ReadAllBytes(GetFullPath(path));

    public override int ReadBytes(string path, byte[] buffer, int offset, int count)
    {
        using var stream = new FileStream(GetFullPath(path), FileMode.Open, FileAccess.Read, FileShare.Read);
        return stream.Read(buffer, offset, count);
    }

    public override IEnumerable<string> EnumerateFiles(string path, bool recursive = false)
    {
        string fullPath = GetFullPath(path);
        if (!Directory.Exists(fullPath)) return [];

        var options = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        return Directory
            .EnumerateFiles(fullPath, "*", options)
            .Select(f => Path.GetRelativePath(SystemRoot, f).Replace('\\', '/'));
    }

    public override IEnumerable<string> EnumerateDirectories(string path, bool recursive = false)
    {
        string fullPath = GetFullPath(path);
        if (!Directory.Exists(fullPath)) return [];

        var options = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

        return Directory.EnumerateDirectories(fullPath, "*", options)
            .Select(d => Path.GetRelativePath(SystemRoot, d).Replace('\\', '/'));
    }

    public override Stream OpenWrite(string path)
    {
        string fullPath = GetFullPath(path);

        string? directory = Path.GetDirectoryName(fullPath);
        if (directory != null) Directory.CreateDirectory(directory);

        return File.Create(fullPath);
    }

    public override void WriteAllBytes(string path, byte[] bytes)
    {
        string fullPath = GetFullPath(path);
        string? directory = Path.GetDirectoryName(fullPath);
        if (directory != null) Directory.CreateDirectory(directory);

        File.WriteAllBytes(fullPath, bytes);
    }

    public override void WriteAllText(string path, string text)
    {
        string fullPath = GetFullPath(path);
        string? directory = Path.GetDirectoryName(fullPath);
        if (directory != null) Directory.CreateDirectory(directory);

        File.WriteAllText(fullPath, text);
    }
}