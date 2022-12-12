namespace Tailwind.MSBuild.UnitTests;

using Xunit;
using FluentAssertions;
using Tailwind.MSBuild.UnitTests.Common;
using Tailwind.MSBuild.GitHub;

public class GitHubClientTests : IClassFixture<GithubClientFixture>
{
	private readonly GithubClientFixture fixture;

	public GitHubClientTests(GithubClientFixture fixture)
	{
		this.fixture = fixture;
	}

	[Fact]
	public async Task GetLatestReleaseAsync_Succeeds()
	{
		var release = await this.fixture.Client.GetLatestReleaseAsync();
		release.Should().NotBeNull();
		release.Assets.Should().NotBeNullOrEmpty();
	}

    [Fact]
    public async Task GetReleaseAsync_Succeeds()
    {
        var tag = "v3.2.4";
        var release = await this.fixture.Client.GetReleaseAsync(tag);
        release.Should().NotBeNull();
        release.Assets.Should().NotBeNullOrEmpty();
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
            ID = 84289937,
            Name = "tailwindcss-linux-x64",
            DownloadUrl = "https://github.com/tailwindlabs/tailwindcss/releases/download/v3.2.4/tailwindcss-linux-x64"
        };

		var destinationFilePath = Path.Combine(Directory.GetCurrentDirectory(), asset.Name);

        File.Exists(destinationFilePath).Should().BeFalse(because: "tests should be idempotent");

		var request = this.fixture.Client.GetAssetAsync(asset, destinationFilePath);
        await request;

        request.IsCompletedSuccessfully.Should().BeTrue();
		File.Exists(destinationFilePath).Should().BeTrue();
		File.Delete(destinationFilePath);
	}

    [Fact]
    public async Task GetAssetAsync_FileExistsOverwriteSucceeds()
    {
        var asset = new TailwindAsset
        {
            ID = 84289937,
            Name = "tailwindcss-linux-x64",
            DownloadUrl = "https://github.com/tailwindlabs/tailwindcss/releases/download/v3.2.4/tailwindcss-linux-x64"
        };

        var testData = "Hello World";
        var destinationFilePath = Path.Combine(Directory.GetCurrentDirectory(), asset.Name);

        File.Exists(destinationFilePath).Should().BeFalse(because: "tests should be idempotent");
        File.WriteAllText(destinationFilePath, testData);

        var request = this.fixture.Client.GetAssetAsync(asset, destinationFilePath);
        await request;

        request.IsCompletedSuccessfully.Should().BeTrue();
        File.Exists(destinationFilePath).Should().BeTrue();
        File.ReadAllText(destinationFilePath).Should().NotBe(testData, because: "client should overwrite existing files at the destination path");

        File.Delete(destinationFilePath);
        File.Exists(destinationFilePath).Should().BeFalse(because: "test should clean up after itself.");
    }
}
