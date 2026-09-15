using System.Text.RegularExpressions;
using Real.Core.Assets.Providers;
using Serilog;

namespace Real.Core.Assets
{
    public static partial class Vfs
    {
        private static readonly Dictionary<string, StorageProvider> _providers = [];

        public static IReadOnlyDictionary<string, StorageProvider> Providers => _providers.AsReadOnly();

        public static void RegisterScheme(string scheme, StorageProvider provider)
        {
            if (!SchemeRegex().IsMatch(scheme))
                throw new ArgumentException($"Invalid scheme: '{scheme}'");
            if (!_providers.TryAdd(scheme, provider))
                throw new ArgumentException($"Duplicate scheme: '{scheme}'");

            Log.Debug("Registered new scheme {Scheme} ({Provider})", $"{scheme}://", provider.GetType().Name);
        }

        public static bool FileExists(string location)
        {
            try
            {
                return FileExists((ResourceLocation)location);
            }
            catch
            {
                return false;
            }
        }

        public static bool FileExists(ResourceLocation location)
        {
            var provider = GetProvider(location.Scheme);
            return provider.FileExists(location.Path);
        }

        public static bool DirectoryExists(string location)
        {
            try
            {
                return DirectoryExists((ResourceLocation)location);
            }
            catch
            {
                return false;
            }
        }

        public static bool DirectoryExists(ResourceLocation location)
        {
            var provider = GetProvider(location.Scheme);
            return provider.DirectoryExists(location.Path);
        }

        public static Stream OpenRead(ResourceLocation location)
        {
            var provider = GetProvider(location.Scheme);
            return provider.OpenRead(location.Path);
        }

        public static Stream OpenWrite(ResourceLocation location)
        {
            var provider = GetProvider(location.Scheme);
            EnsureWritable(location.Scheme, provider);
            return provider.OpenWrite(location.Path);
        }

        public static byte[] ReadAllBytes(ResourceLocation location)
        {
            var provider = GetProvider(location.Scheme);
            return provider.ReadAllBytes(location.Path);
        }

        public static int ReadBytes(ResourceLocation location, byte[] buffer, int offset, int count)
        {
            var provider = GetProvider(location.Scheme);
            return provider.ReadBytes(location.Path, buffer, offset, count);
        }

        public static byte[] ReadBytes(ResourceLocation location, int offset, int count)
        {
            var provider = GetProvider(location.Scheme);
            return provider.ReadBytes(location.Path, offset, count);
        }

        public static string ReadAllText(ResourceLocation location)
        {
            var provider = GetProvider(location.Scheme);
            return provider.ReadAllText(location.Path);
        }

        public static void WriteAllText(ResourceLocation location, string text)
        {
            var provider = GetProvider(location.Scheme);
            EnsureWritable(location.Scheme, provider);
            provider.WriteAllText(location.Path, text);
        }

        public static void WriteAllBytes(ResourceLocation location, byte[] bytes)
        {
            var provider = GetProvider(location.Scheme);
            EnsureWritable(location.Scheme, provider);
            provider.WriteAllBytes(location.Path, bytes);
        }

        public static IEnumerable<ResourceLocation> EnumerateFiles(ResourceLocation location, bool recursive = false)
        {
            var provider = GetProvider(location.Scheme);
            return provider.EnumerateFiles(location.Path, recursive)
                .Select(path => new ResourceLocation(location.Scheme, path));
        }

        public static IEnumerable<ResourceLocation> EnumerateFiles(ResourceLocation location, string extension,
            bool recursive = false)
        {
            return EnumerateFiles(location, recursive).Where(file =>
                file.Path.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
        }

        public static IEnumerable<ResourceLocation> EnumerateDirectories(ResourceLocation location,
            bool recursive = false)
        {
            var provider = GetProvider(location.Scheme);
            return provider.EnumerateDirectories(location.Path, recursive)
                .Select(path => new ResourceLocation(location.Scheme, path));
        }

        private static StorageProvider GetProvider(string scheme)
        {
            if (!_providers.TryGetValue(scheme, out var provider))
                throw new ArgumentException($"Invalid scheme: '{scheme}'");
            return provider;
        }

        private static void EnsureWritable(string scheme, StorageProvider provider)
        {
            if (provider.IsReadOnly)
                throw new NotSupportedException(
                    $"Writable scheme '{scheme}' is not supported (Provider is read-only).");
        }

        [GeneratedRegex("^[a-z0-9_]+$")]
        private static partial Regex SchemeRegex();
    }
}