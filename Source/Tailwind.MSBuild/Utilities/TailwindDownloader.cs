namespace Tailwind.MSBuild.Utilities;

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

        var packageVersion = typeof(TailwindDownloader).Assembly
                                                       .GetCustomAttribute<AssemblyFileVersionAttribute>()?
                                                       .Version
                                                       ?? "0.0.0";

        // The GitHub REST API requires a user-agent header for unauthenticated requests.
        this.client.DefaultRequestHeaders.Add("User-Agent", $"Tailwind.MSBuild/{packageVersion} mjrasicci.dev");
    }

    /// <summary>
    ///     Get the published release with the specified tag. If no tag is specified, gets the latest release.
    /// </summary>
    /// <returns>
    ///     A <see cref="GitHubReleaseResponse" /> for the specified version of TailwindCSS.
    /// </returns>
    public async Task<GitHubReleaseResponse?> GetReleaseAsync(string? tag = null)
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
        return JsonSerializer.Deserialize<GitHubReleaseResponse>(response);
    }

    /// <summary>
    ///     Downloads a <see cref="GitHubReleaseAsset" />.
    /// </summary>
    /// <param name="tailwindBinary">
    ///     The platform specific binary to download.
    /// </param>
    /// <returns>
    ///     A byte array containing the asset.
    /// </returns>
    public async Task<byte[]> GetAssetAsync(GitHubReleaseAsset tailwindBinary) => await this.client.GetByteArrayAsync(tailwindBinary.DownloadUrl);
}

/// <summary>
///     A small response model for the GitHub Releases API.
/// </summary>
internal struct GitHubReleaseResponse
{
    /// <summary>
    ///     A collection of pre-built binaries for this release.
    /// </summary>
    [JsonPropertyName("assets")]
    public IEnumerable<GitHubReleaseAsset> Assets { get; set; }
}

/// <summary>
///     A small model containing the download information for a release asset.
/// </summary>
internal struct GitHubReleaseAsset
{
    /// <summary>
    ///     The address to download the asset's binary content.
    /// </summary>
    [JsonPropertyName("browser_download_url")]
    public string DownloadUrl { get; set; }

    /// <summary>
    ///     The name of the download.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; }
}
