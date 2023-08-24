namespace Tailwind.MSBuild.GitHub;

/// <summary>
///     Provides a basic client to download the Tailwind Standalone CLI from Github.
/// </summary>
internal class TailwindDownloader : IDisposable
{
    private readonly HttpClient client;

    private bool disposed;

    /// <inheritdoc cref="Dispose()" />
    protected virtual void Dispose(bool disposing)
    {
        if (!this.disposed)
        {
            if (disposing)
                this.client.Dispose();

            this.disposed = true;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="TailwindDownloader" /> class.
    /// </summary>
    public TailwindDownloader()
    {
        this.client = new HttpClient();

        // The GitHub REST API requires a user-agent header for unauthenticated requests.
        this.client.DefaultRequestHeaders.Add("User-Agent", "Tailwind.MSBuild/1.1.0 mjrasicci.dev");
    }

    /// <summary>
    ///     Get the published release with the specified tag. If no tag is specified, gets the latest release.
    /// </summary>
    /// <returns>
    ///     A <see cref="TailwindRelease" /> for the specified version of TailwindCSS.
    /// </returns>
    public async Task<TailwindRelease?> GetReleaseAsync(string? tag = null)
    {
        var requestUri = "https://api.github.com/repos/tailwindlabs/tailwindcss/releases";

        if (tag is "latest" or null)
        {
            requestUri = $"{requestUri}/latest";
        }
        else
        {
            requestUri = $"{requestUri}/tags/{tag}";
        }

        var response = await this.client.GetStringAsync(requestUri);
        return JsonSerializer.Deserialize<TailwindRelease>(response);
    }

    /// <summary>
    ///     Downloads a <see cref="TailwindAsset" />.
    /// </summary>
    /// <param name="tailwindBinary">
    ///     The platform specific binary to download.
    /// </param>
    /// <returns>
    ///     A byte array containing the asset.
    /// </returns>
    public async Task<byte[]> GetAssetAsync(TailwindAsset tailwindBinary) => await this.client.GetByteArrayAsync(tailwindBinary.DownloadUrl);
}


/// <summary>
///     A lightweight response model for the GitHub Releases API.
/// </summary>
internal class TailwindRelease
{
    /// <summary>
    ///     A collection of pre-built binaries for this release.
    /// </summary>
    [JsonPropertyName("assets")]
    public IEnumerable<TailwindAsset> Assets { get; set; } = Array.Empty<TailwindAsset>();
}

/// <summary>
///     A small model containing the download information for a release asset.
/// </summary>
internal class TailwindAsset
{
    /// <summary>
    ///     The address to download the asset's binary content.
    /// </summary>
    [JsonPropertyName("browser_download_url")]
    public string DownloadUrl { get; set; } = string.Empty;

    /// <summary>
    ///     The name of the download.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}
