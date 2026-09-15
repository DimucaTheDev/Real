using System.Reflection;

namespace Real.Core.Assets.Providers
{
    public class AssemblyStorageProvider : StorageProvider
    {
        private readonly Assembly _assembly;
        private readonly string _assemblyName;

        private readonly Dictionary<string, string> _vfsToManifestMap = new(StringComparer.OrdinalIgnoreCase);

        private readonly HashSet<string> _directories = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, HashSet<string>> _dirToFiles = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, HashSet<string>> _dirToSubDirs = new(StringComparer.OrdinalIgnoreCase);

        public AssemblyStorageProvider(Assembly assembly)
        {
            _assembly = assembly ?? throw new ArgumentNullException(nameof(assembly));
            _assemblyName = assembly.GetName().Name ?? string.Empty;

            BuildVirtualFileSystem();
        }

        public static void RegisterNew(Assembly assembly)
        {
            string assemblyName = assembly.GetName().Name ?? string.Empty;
            if (!string.IsNullOrEmpty(assemblyName))
            {
                string scheme = assemblyName.ToLowerInvariant();

                var assemblyProvider = new AssemblyStorageProvider(assembly);
                Vfs.RegisterScheme(scheme, assemblyProvider);

                //Log.Debug("Registered VFS scheme '{Scheme}://' for plugin assembly", scheme);
            }
        }

        private void BuildVirtualFileSystem()
        {
            var resourceNames = _assembly.GetManifestResourceNames();

            foreach (var manifestName in resourceNames)
            {
                var relativePath = manifestName;
                if (!string.IsNullOrEmpty(_assemblyName) &&
                    manifestName.StartsWith(_assemblyName + ".", StringComparison.OrdinalIgnoreCase))
                {
                    relativePath = manifestName[(_assemblyName.Length + 1)..];
                }

                var vfsPath = ConvertManifestPathToVfsPath(relativePath);

                _vfsToManifestMap[vfsPath] = manifestName;

                var dirPath = Path.GetDirectoryName(vfsPath)?.Replace('\\', '/').Trim('/');
                dirPath = string.IsNullOrEmpty(dirPath) ? "" : dirPath;

                if (!_dirToFiles.TryGetValue(dirPath, out var files))
                {
                    files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    _dirToFiles[dirPath] = files;
                }

                files.Add(vfsPath);

                var parts = dirPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                var currentAccumulatedPath = "";

                for (var i = 0; i < parts.Length; i++)
                {
                    var parent = currentAccumulatedPath;
                    currentAccumulatedPath = string.IsNullOrEmpty(currentAccumulatedPath)
                        ? parts[i]
                        : $"{currentAccumulatedPath}/{parts[i]}";

                    _directories.Add(currentAccumulatedPath);

                    if (!_dirToSubDirs.TryGetValue(parent, out var subDirs))
                    {
                        subDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        _dirToSubDirs[parent] = subDirs;
                    }

                    subDirs.Add(currentAccumulatedPath);
                }
            }
        }

        private static string ConvertManifestPathToVfsPath(string manifestPath)
        {
            var lastDotIdx = manifestPath.LastIndexOf('.');
            if (lastDotIdx == -1)
                return manifestPath;

            var chars = manifestPath.ToCharArray();
            for (var i = 0; i < lastDotIdx; i++)
            {
                if (chars[i] == '.') chars[i] = '/';
            }

            return new string(chars);
        }

        private string NormalizePath(string path)
        {
            return path.Replace('\\', '/').Trim('/');
        }

        public override bool FileExists(string path)
        {
            return _vfsToManifestMap.ContainsKey(NormalizePath(path));
        }

        public override bool DirectoryExists(string path)
        {
            var normalized = NormalizePath(path);
            if (normalized == string.Empty) return true;
            return _directories.Contains(normalized);
        }

        public override Stream OpenRead(string path)
        {
            var normalized = NormalizePath(path);
            if (!_vfsToManifestMap.TryGetValue(normalized, out var manifestName))
            {
                throw new FileNotFoundException($"Resource not found in assembly: {path}");
            }

            return _assembly.GetManifestResourceStream(manifestName)
                   ?? throw new FileNotFoundException($"Failed to open resource stream: {manifestName}");
        }

        public override byte[] ReadAllBytes(string path)
        {
            using var stream = OpenRead(path);
            var buffer = new byte[stream.Length];

            var totalBytesRead = 0;
            while (totalBytesRead < buffer.Length)
            {
                var read = stream.Read(buffer, totalBytesRead, buffer.Length - totalBytesRead);
                if (read == 0) break;
                totalBytesRead += read;
            }

            return buffer;
        }

        public override int ReadBytes(string path, byte[] buffer, int offset, int count)
        {
            using var stream = OpenRead(path);
            return stream.Read(buffer, offset, count);
        }

        public override IEnumerable<string> EnumerateFiles(string path, bool recursive = false)
        {
            var normalized = NormalizePath(path);

            if (recursive)
            {
                return _vfsToManifestMap.Keys.Where(f =>
                    normalized == string.Empty || f.StartsWith(normalized + "/", StringComparison.OrdinalIgnoreCase));
            }

            if (_dirToFiles.TryGetValue(normalized, out var files))
            {
                return files;
            }

            return Enumerable.Empty<string>();
        }

        public override IEnumerable<string> EnumerateDirectories(string path, bool recursive = false)
        {
            var normalized = NormalizePath(path);

            if (recursive)
            {
                return _directories.Where(d =>
                    normalized == string.Empty || d.StartsWith(normalized + "/", StringComparison.OrdinalIgnoreCase));
            }

            if (_dirToSubDirs.TryGetValue(normalized, out var subDirs))
            {
                return subDirs;
            }

            return Enumerable.Empty<string>();
        }
    }
}