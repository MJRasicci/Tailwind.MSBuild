namespace Tailwind.MSBuild.Tests;

using Xunit;
using FluentAssertions;
using Tailwind.MSBuild.Utilities;
using Tailwind.MSBuild.Tests.Common;

public class TailwindDownloaderTests : IClassFixture<TailwindDownloaderFixture>
{
    private readonly TailwindDownloaderFixture fixture;

    public TailwindDownloaderTests(TailwindDownloaderFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task GetLatestReleaseAsync_Succeeds()
    {
        var release = await this.fixture.Client.GetReleaseAsync();
        release.Should().NotBeNull();
        release?.Assets.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetReleaseAsync_Succeeds()
    {
        var release = await this.fixture.Client.GetReleaseAsync("v4.1.18");
        release.Should().NotBeNull();
        release?.Assets.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetReleaseAsync_InvalidTagFails()
    {
        var tag = "42069";
        var request = async () => await this.fixture.Client.GetReleaseAsync(tag);
        await request.Should().ThrowAsync<HttpRequestException>(because: $"'{tag}' is not a valid semver tag.");
    }

    [Fact]
    public async Task GetAssetAsync_Succeeds()
    {
        var asset = new GitHubReleaseAsset
        {
            Name = "tailwindcss-linux-x64",
            DownloadUrl = "https://github.com/tailwindlabs/tailwindcss/releases/download/v4.1.18/tailwindcss-linux-x64"
        };

        var request = this.fixture.Client.GetAssetAsync(asset);
        var result = await request;

        request.IsCompletedSuccessfully.Should().BeTrue();
        result.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ParseRelease_PopulatesBrowserDownloadUrl()
    {
        const string json = """
            {
              "tag_name": "v4.2.2",
              "assets": [
                {
                  "name": "tailwindcss-windows-x64.exe",
                  "url": "https://api.github.com/repos/tailwindlabs/tailwindcss/releases/assets/376534859",
                  "browser_download_url": "https://github.com/tailwindlabs/tailwindcss/releases/download/v4.2.2/tailwindcss-windows-x64.exe"
                }
              ]
            }
            """;

        var release = TailwindDownloader.ParseRelease(json);
        var asset = release.Assets.Should().ContainSingle().Subject;

        release.TagName.Should().Be("v4.2.2");
        asset.Name.Should().Be("tailwindcss-windows-x64.exe");
        asset.AssetApiUrl.Should().Be("https://api.github.com/repos/tailwindlabs/tailwindcss/releases/assets/376534859");
        asset.DownloadUrl.Should().Be("https://github.com/tailwindlabs/tailwindcss/releases/download/v4.2.2/tailwindcss-windows-x64.exe");
    }

    [Fact]
    public async Task GetAssetAsync_InvalidUrlsFailClearly()
    {
        var asset = new GitHubReleaseAsset
        {
            Name = "tailwindcss-windows-x64.exe"
        };

        var request = async () => await this.fixture.Client.GetAssetAsync(asset);

        await request.Should()
                     .ThrowAsync<InvalidOperationException>()
                     .WithMessage("Unable to determine a valid download URL*");
    }
}
