namespace Tailwind.MSBuild.GitHub;

/// <summary>
///     Provides a basic client to download the Tailwind Standalone CLI from Github.
/// </summary>
internal class GitHubClient : IDisposable
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
    ///     Initializes a new instance of the <see cref="GitHubClient" /> class.
    /// </summary>
    public GitHubClient()
    {
        this.client = new HttpClient();

        // The GitHub REST API requires a user-agent header for unauthenticated requests.
        this.client.DefaultRequestHeaders.Add("User-Agent", "Tailwind.MSBuild/1.0.0 mjrasicci.dev");
    }

    /// <summary>
    ///     Get the latest published full release for the repository.
    /// </summary>
    /// <returns>
    ///     A <see cref="TailwindRelease" /> object for the latest version of TailwindCSS.
    /// </returns>
    public async Task<TailwindRelease?> GetLatestReleaseAsync()
    {
        var response = await this.client.GetStringAsync("https://api.github.com/repos/mjrasicci/tailwindcss/releases/latest");
        return JsonSerializer.Deserialize<TailwindRelease>(response);
    }

    /// <summary>
    ///     Get the published release with the specified tag.
    /// </summary>
    /// <returns>
    ///     A <see cref="TailwindRelease" /> for the specified version of TailwindCSS.
    /// </returns>
    public async Task<TailwindRelease?> GetReleaseAsync(string tag)
    {
        var response = await this.client.GetStringAsync($"https://api.github.com/repos/mjrasicci/tailwindcss/releases/tags/{tag}");
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
