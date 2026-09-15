using System.IO.Compression;
using Serilog;

namespace Real.Core.Assets.Providers;

public class PakStorageProvider : StorageProvider, IDisposable
{
    public override bool IsReadOnly => true;

    private readonly Dictionary<string, ZipArchive> _zips = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ZipArchiveEntry> _fileMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _originalPathMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _directoryCache = new(StringComparer.OrdinalIgnoreCase);

    public PakStorageProvider(string rootDirectory = "Engine/Content")
    {
        MountDirectory(rootDirectory);
    }

    public void MountDirectory(string directory)
    {
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
            return;
        }

        foreach (var pakPath in Directory.EnumerateFiles(directory, "*.pak"))
        {
            MountPak(pakPath);
        }
    }

    public void MountPak(string pakPath)
    {
        try
        {
            var fs = File.OpenRead(pakPath);
            var zip = new ZipArchive(fs, ZipArchiveMode.Read, leaveOpen: false);
            _zips[pakPath] = zip;

            RegisterEntries(zip);
            Log.Information("Mounted PAK {Path}", Path.GetRelativePath(".", pakPath));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to mount PAK: {PakPath}", pakPath);
        }
    }

    private void RegisterEntries(ZipArchive zip)
    {
        foreach (var entry in zip.Entries)
        {
            string normalizedEntry = entry.FullName.Replace('\\', '/');

            if (normalizedEntry.EndsWith('/'))
            {
                string dirPath = normalizedEntry.TrimEnd('/');
                RegisterDirectoryPath(dirPath);
                continue;
            }

            string key = NormalizePath(normalizedEntry);

            if (!_fileMap.TryAdd(key, entry))
            {
                Log.Error("Duplicate file {FileName} in PAKs.", key);
            }
            else
            {
                _originalPathMap[key] = normalizedEntry;
            }

            int lastSlash = normalizedEntry.LastIndexOf('/');
            if (lastSlash > 0)
            {
                string directoryPath = normalizedEntry[..lastSlash];
                RegisterDirectoryPath(directoryPath);
            }
        }
    }

    private void RegisterDirectoryPath(string directoryPath)
    {
        var parts = directoryPath.Split('/');
        string currentPath = "";
        foreach (var part in parts)
        {
            currentPath = string.IsNullOrEmpty(currentPath) ? part : $"{currentPath}/{part}";
            _directoryCache.Add(NormalizePath(currentPath));
        }
    }

    public override bool FileExists(string path)
    {
        return _fileMap.ContainsKey(NormalizePath(path));
    }

    public override bool DirectoryExists(string path)
    {
        string norm = NormalizePath(path);
        if (string.IsNullOrEmpty(norm)) return true;
        return _directoryCache.Contains(norm);
    }

    public override Stream OpenRead(string path)
    {
        var entry = GetEntryOrThrow(path);

        using var stream = entry.Open();
        var ms = new MemoryStream((int)entry.Length);
        stream.CopyTo(ms);
        ms.Position = 0;
        return ms;
    }

    public override byte[] ReadAllBytes(string path)
    {
        var entry = GetEntryOrThrow(path);

        using var stream = entry.Open();
        using var ms = new MemoryStream((int)entry.Length);
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    public override int ReadBytes(string path, byte[] buffer, int offset, int count)
    {
        var entry = GetEntryOrThrow(path);

        using var stream = entry.Open();
        int totalRead = 0;
        while (totalRead < count)
        {
            int readBytes = stream.Read(buffer, offset + totalRead, count - totalRead);
            if (readBytes <= 0) break;
            totalRead += readBytes;
        }

        return totalRead;
    }

    public override IEnumerable<string> EnumerateFiles(string path, bool recursive = false)
    {
        string normPath = NormalizePath(path);
        string prefix = string.IsNullOrEmpty(normPath) ? "" : normPath + "/";

        foreach (var (key, originalPath) in _originalPathMap)
        {
            if (!string.IsNullOrEmpty(prefix) && !key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;

            string relative = string.IsNullOrEmpty(prefix) ? key : key[prefix.Length..];

            if (!recursive && relative.Contains('/'))
                continue;

            yield return originalPath;
        }
    }

    public override IEnumerable<string> EnumerateDirectories(string path, bool recursive = false)
    {
        string normPath = NormalizePath(path);
        string prefix = string.IsNullOrEmpty(normPath) ? "" : normPath + "/";

        foreach (var dir in _directoryCache)
        {
            if (!string.IsNullOrEmpty(prefix) && !dir.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;

            string relative = string.IsNullOrEmpty(prefix) ? dir : dir[prefix.Length..];

            if (!recursive && relative.Contains('/'))
                continue;

            yield return dir;
        }
    }

    private ZipArchiveEntry GetEntryOrThrow(string path)
    {
        string key = NormalizePath(path);
        if (!_fileMap.TryGetValue(key, out var entry))
            throw new FileNotFoundException($"File not found in PAK storage: {path}");

        return entry;
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrEmpty(path)) return string.Empty;
        return path.Replace('\\', '/').Trim('/');
    }

    public void Dispose()
    {
        foreach (var zip in _zips.Values)
        {
            zip.Dispose();
        }
        _zips.Clear();
        _fileMap.Clear();
        _originalPathMap.Clear();
        _directoryCache.Clear();
    }
}