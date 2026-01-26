namespace Tailwind.MSBuild.Utilities;

using System.Text;

/// <summary>
///     Represents the contents of the Tailwind CLI lock file.
/// </summary>
internal sealed class TailwindLockFile
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>
    ///     The lock file entries stored in the file.
    /// </summary>
    public List<TailwindLockFileEntry> Entries { get; } = [];

    /// <summary>
    ///     Initializes a new instance of the <see cref="TailwindLockFile" /> class.
    /// </summary>
    public TailwindLockFile()
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="TailwindLockFile" /> class.
    /// </summary>
    public TailwindLockFile(IEnumerable<TailwindLockFileEntry> entries)
    {
        this.Entries = [.. entries ?? []];
    }

    /// <summary>
    ///     Reads a lock file from the provided stream.
    /// </summary>
    public static TailwindLockFile Read(Stream stream, out bool isCorrupt)
    {
        isCorrupt = false;

        if (stream.Length == 0)
            return new TailwindLockFile();

        stream.Position = 0;

        using var reader = new StreamReader(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), true, 1024, leaveOpen: true);
        var json = reader.ReadToEnd();

        if (string.IsNullOrWhiteSpace(json))
            return new TailwindLockFile();

        try
        {
            var entries = JsonSerializer.Deserialize<List<TailwindLockFileEntry>>(json, SerializerOptions) ?? [];

            return new TailwindLockFile(entries);
        }
        catch (JsonException)
        {
            isCorrupt = true;
            return new TailwindLockFile();
        }
    }

    /// <summary>
    ///     Writes the lock file contents to the provided stream.
    /// </summary>
    public void Write(Stream stream)
    {
        stream.SetLength(0);
        stream.Position = 0;

        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), 1024, leaveOpen: true);
        var json = JsonSerializer.Serialize(this.Entries, SerializerOptions);
        writer.Write(json);
        writer.Flush();
        stream.Flush();
    }
}

/// <summary>
///     Represents a single Tailwind CLI instance in the lock file.
/// </summary>
internal sealed class TailwindLockFileEntry
{
    /// <summary>
    ///     The process id for the Tailwind CLI instance.
    /// </summary>
    [JsonPropertyName("pid")]
    public int ProcessId { get; set; }

    /// <summary>
    ///     When the process was started, in UTC.
    /// </summary>
    [JsonPropertyName("startedAtUtc")]
    public DateTimeOffset StartedAtUtc { get; set; }

    /// <summary>
    ///     The project directory for the Tailwind CLI instance.
    /// </summary>
    [JsonPropertyName("projectDirectory")]
    public string ProjectDirectory { get; set; } = string.Empty;
}
