namespace Tailwind.MSBuild.GitHub;

/// <summary>
///     A lightweight response model for the GitHub Releases API.
/// </summary>
internal class TailwindRelease
{
    /// <summary>
    ///     A unique identifier for this release.
    /// </summary>
    [JsonPropertyName("id")]
    public int ID { get; set; }

    /// <summary>
    ///     The semver string of this release as labeled by the release tag name.
    /// </summary>
    [JsonPropertyName("tag_name")]
    public string Version { get; set; } = string.Empty;

    /// <summary>
    ///     The web address for the release.
    /// </summary>
    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; set; } = string.Empty;

    /// <summary>
    ///     A collection of pre-built binaries for this release.
    /// </summary>
    [JsonPropertyName("assets")]
    public IEnumerable<TailwindAsset> Assets { get; set; } = Array.Empty<TailwindAsset>();
}
