namespace Tailwind.MSBuild.GitHub;

using System.Text.Json.Serialization;

/// <summary>
///     A small model containing the download information for a release asset.
/// </summary>
internal class TailwindAsset
{
    /// <summary>
    ///     A unique identifier for this asset.
    /// </summary>
    [JsonPropertyName("id")]
    public int ID { get; set; }

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
