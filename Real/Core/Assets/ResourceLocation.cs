using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Real.Core.Assets;

[JsonConverter(typeof(ResourceLocationJsonConverter))]
public sealed partial class ResourceLocation : IEquatable<ResourceLocation>
{
    public string Scheme { get; private set; } = string.Empty;
    public string Path { get; private set; } = string.Empty;

    private string _fullPath = string.Empty;

    private ResourceLocation()
    {
    }

    public ResourceLocation(string scheme, string path)
    {
        if (string.IsNullOrWhiteSpace(scheme))
            throw new ArgumentException("Scheme cannot be null or empty.", nameof(scheme));

        Scheme = scheme.ToLowerInvariant();

        if (!SchemeRegex().IsMatch(Scheme))
            throw new ArgumentException($"Invalid scheme format: '{scheme}'. Only a-z, 0-9 and '_' are allowed.",
                nameof(scheme));

        Path = path.Replace('\\', '/').Trim('/');

        if (Path.Contains("://", StringComparison.Ordinal))
            throw new ArgumentException("Path part cannot contain '://' separator.", nameof(path));

        _fullPath = $"{Scheme}://{Path}";
    }

    public static ResourceLocation Parse(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        int separatorIndex = path.IndexOf("://", StringComparison.Ordinal);

        if (separatorIndex <= 0)
        {
            throw new ArgumentException(
                $"Invalid resource path: '{path}'. Paths must start with a valid scheme followed by '://'.",
                nameof(path));
        }

        var location = new ResourceLocation
        {
            Scheme = path[..separatorIndex].ToLowerInvariant()
        };

        if (!SchemeRegex().IsMatch(location.Scheme))
        {
            throw new ArgumentException(
                $"Invalid scheme '{location.Scheme}' in path '{path}'. Only a-z, 0-9 and '_' are allowed.",
                nameof(path));
        }

        string rawPath = path[(separatorIndex + 3)..];
        location.Path = rawPath.Replace('\\', '/').Trim('/');

        if (string.IsNullOrWhiteSpace(location.Path))
        {
            throw new ArgumentException(
                $"Invalid resource path: '{path}'. Path part cannot be empty.", nameof(path));
        }

        if (location.Path.Contains("://", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Invalid resource path: '{path}'. Multiple '://' separators are not allowed.", nameof(path));
        }

        location._fullPath = $"{location.Scheme}://{location.Path}";

        return location;
    }

    public override string ToString() => _fullPath;
    public override bool Equals(object? obj) => obj is ResourceLocation other && Equals(other);

    public bool Equals(ResourceLocation? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return string.Equals(_fullPath, other._fullPath, StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex("^[a-z0-9_]+$")]
    private static partial Regex SchemeRegex();

    public override int GetHashCode() => string.GetHashCode(_fullPath, StringComparison.OrdinalIgnoreCase);

    public static bool operator ==(ResourceLocation? left, ResourceLocation? right) => Equals(left, right);
    public static bool operator !=(ResourceLocation? left, ResourceLocation? right) => !Equals(left, right);

    public static implicit operator ResourceLocation(string location) => Parse(location);

    private class ResourceLocationJsonConverter : JsonConverter<ResourceLocation>
    {
        public override ResourceLocation? Read(ref Utf8JsonReader reader, Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            if (reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException($"Expected string token for {nameof(ResourceLocation)}, but got {reader.TokenType}.");
            }

            string? path = reader.GetString();
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            try
            {
                return ResourceLocation.Parse(path);
            }
            catch (ArgumentException ex)
            {
                throw new JsonException($"Failed to parse {nameof(ResourceLocation)} from JSON value '{path}'.", ex);
            }
        }

        public override void Write(Utf8JsonWriter writer, ResourceLocation? value, JsonSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNullValue();
            }
            else
            {
                writer.WriteStringValue(value.ToString());
            }
        }
    }
}