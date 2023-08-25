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
        var release = await this.fixture.Client.GetReleaseAsync("v3.2.4");
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
        var asset = new TailwindAsset
        {
            Name = "tailwindcss-linux-x64",
            DownloadUrl = "https://github.com/tailwindlabs/tailwindcss/releases/download/v3.2.4/tailwindcss-linux-x64"
        };

        var request = this.fixture.Client.GetAssetAsync(asset);
        var result = await request;

        request.IsCompletedSuccessfully.Should().BeTrue();
        result.Length.Should().BeGreaterThan(0);
    }
}
