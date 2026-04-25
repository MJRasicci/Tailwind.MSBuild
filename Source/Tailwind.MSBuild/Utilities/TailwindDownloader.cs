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
        return ParseRelease(response);
    }

    internal static GitHubReleaseResponse ParseRelease(string response)
    {
        using var document = JsonDocument.Parse(response);

        var release = new GitHubReleaseResponse();
        var root = document.RootElement;

        if (root.TryGetProperty("tag_name", out var tagNameProperty))
            release.TagName = tagNameProperty.GetString() ?? string.Empty;

        if (!root.TryGetProperty("assets", out var assetsProperty) || assetsProperty.ValueKind != JsonValueKind.Array)
            return release;

        foreach (var assetProperty in assetsProperty.EnumerateArray())
        {
            release.Assets.Add(new GitHubReleaseAsset
            {
                Name = GetString(assetProperty, "name"),
                DownloadUrl = GetString(assetProperty, "browser_download_url"),
                AssetApiUrl = GetString(assetProperty, "url")
            });
        }

        return release;
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
    public async Task<byte[]> GetAssetAsync(GitHubReleaseAsset tailwindBinary)
    {
        if (TryCreateAbsoluteUri(tailwindBinary.DownloadUrl, out var browserDownloadUri))
            return await this.client.GetByteArrayAsync(browserDownloadUri);

        if (!TryCreateAbsoluteUri(tailwindBinary.AssetApiUrl, out var assetApiUri))
        {
            throw new InvalidOperationException(
                $"Unable to determine a valid download URL for '{tailwindBinary.Name}'. " +
                $"GitHub returned browser_download_url='{tailwindBinary.DownloadUrl}' and url='{tailwindBinary.AssetApiUrl}'.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, assetApiUri);
        request.Headers.TryAddWithoutValidation("Accept", "application/octet-stream");

        using var response = await this.client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync();
    }

    private static string GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
             ? property.GetString() ?? string.Empty
             : string.Empty;
    }

    private static bool TryCreateAbsoluteUri(string uriString, out Uri uri)
    {
        return Uri.TryCreate(uriString, UriKind.Absolute, out uri!);
    }
}

/// <summary>
///     A small response model for the GitHub Releases API.
/// </summary>
internal sealed class GitHubReleaseResponse
{
    /// <summary>
    ///     The release tag such as v4.2.2.
    /// </summary>
    public string TagName { get; set; } = string.Empty;

    /// <summary>
    ///     A collection of pre-built binaries for this release.
    /// </summary>
    public List<GitHubReleaseAsset> Assets { get; } = [];
}

/// <summary>
///     A small model containing the download information for a release asset.
/// </summary>
internal sealed class GitHubReleaseAsset
{
    /// <summary>
    ///     The address to download the asset's binary content.
    /// </summary>
    public string DownloadUrl { get; set; } = string.Empty;

    /// <summary>
    ///     The GitHub API address for the release asset.
    /// </summary>
    public string AssetApiUrl { get; set; } = string.Empty;

    /// <summary>
    ///     The name of the download.
    /// </summary>
    public string Name { get; set; } = string.Empty;
}
