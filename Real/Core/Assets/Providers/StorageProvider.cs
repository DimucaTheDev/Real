using System.Text;
using Serilog;

namespace Real.Core.Assets.Providers;

public abstract class StorageProvider
{
    public virtual bool IsReadOnly => true;

    public abstract bool FileExists(string path);
    public abstract bool DirectoryExists(string path);

    public abstract Stream OpenRead(string path);

    public abstract byte[] ReadAllBytes(string path);
    public abstract int ReadBytes(string path, byte[] buffer, int offset, int count);

    public virtual byte[] ReadBytes(string path, int offset, int count)
    {
        var buffer = new byte[count];
        ReadBytes(path, buffer, offset, count);
        return buffer;
    }

    public virtual string ReadAllText(string path)
    {
        byte[] bytes = ReadAllBytes(path);

        ReadOnlySpan<byte> span = bytes;
        if (span.StartsWith(new ReadOnlySpan<byte>([0xEF, 0xBB, 0xBF]))) //BOM
        {
            span = span[3..];
            Log.Verbose("Removed BOM in {Path}", path);
            return Encoding.UTF8.GetString(span);
        }

        return Encoding.UTF8.GetString(bytes);
    }

    public abstract IEnumerable<string> EnumerateFiles(string path, bool recursive = false);
    public abstract IEnumerable<string> EnumerateDirectories(string path, bool recursive = false);

    public virtual void WriteAllBytes(string path, byte[] bytes) => throw new NotSupportedException();
    public virtual void WriteAllText(string path, string text) => throw new NotSupportedException();
    public virtual Stream OpenWrite(string path) => throw new NotSupportedException();
}