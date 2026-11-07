namespace Tailwind.MSBuild.Tests;

using Xunit;
using Tailwind.MSBuild.Tests.Common;
using Tailwind.MSBuild.Tasks;
using Tailwind.MSBuild.Utilities;
using FluentAssertions;
using System.Net;
using System.Net.Http;
using System.Threading;

public class GetTailwindCliTests : IClassFixture<TaskFixture<GetTailwindCLI>>
{
    private const string WindowsArm64 = "tailwindcss-windows-arm64.exe";
    private const string WindowsX64 = "tailwindcss-windows-x64.exe";

    private readonly TaskFixture<GetTailwindCLI> fixture;

    public GetTailwindCliTests(TaskFixture<GetTailwindCLI> fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public void GetTailwindCli_Succeeds()
    {
        var getTailwindCli = this.fixture.Prepare(options =>
        {
            options.Version = "latest";
            options.RootInstallPath = $"./{Guid.NewGuid()}/";
        });

        var success = getTailwindCli.Execute();

        success.Should().BeTrue();
        File.Exists(getTailwindCli.StandaloneCliPath).Should().BeTrue();

        // Make sure the file is able to be executed (checks Posix file permissions)
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = getTailwindCli.StandaloneCliPath,
                Arguments = $"-h",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };

        process.Start();
        process.WaitForExit();
        process.ExitCode.Should().Be(0);

        DeleteDirectory(getTailwindCli.RootInstallPath);
    }

    [Fact]
    public void GetTailwindCli_UsesCache()
    {
        var getTailwindCli = this.fixture.Prepare(options =>
        {
            options.Version = "v4.1.18";
            options.RootInstallPath = $"./{Guid.NewGuid()}/";
        });

        var filePath = getTailwindCli.GetFilePath();

        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

        using (var file = File.CreateText(filePath))
        {
            file.Write("TEST");
            file.Close();
        }

        var success = getTailwindCli.Execute();
        
        success.Should().BeTrue();
        File.Exists(getTailwindCli.StandaloneCliPath).Should().BeTrue();

        if (File.ReadAllText(getTailwindCli.StandaloneCliPath) != "TEST")
            Assert.Fail("File was overwritten");

        DeleteDirectory(getTailwindCli.RootInstallPath);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void Execute_WindowsArm64_SelectsFromRequestedRelease(bool hasNativeAsset, bool hasCachedX64)
    {
        const string version = "v3.4.17";
        var releasePath = $"/repos/tailwindlabs/tailwindcss/releases/tags/{version}";
        var requestedPaths = new List<string>();
        var task = PrepareWindowsArm64(version, path =>
        {
            requestedPaths.Add(path);
            return path == releasePath
                ? CreateRelease(version, hasNativeAsset)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Path.GetFileName(path)) };
        });

        try
        {
            if (hasCachedX64)
                SeedCache(task, version, WindowsX64, "cached x64");

            task.Execute().Should().BeTrue();

            var selected = hasNativeAsset ? WindowsArm64 : WindowsX64;
            task.GetFilePath().Should().Be(GetCachePath(task, version, WindowsArm64));
            task.StandaloneCliPath.Should().Be(GetCachePath(task, version, selected));
            File.ReadAllText(task.StandaloneCliPath).Should().Be(!hasNativeAsset && hasCachedX64 ? "cached x64" : selected);
            requestedPaths.Should().Equal(!hasNativeAsset && hasCachedX64
                ? [releasePath]
                : [releasePath, $"/{selected}"]);
        }
        finally
        {
            DeleteDirectory(task.RootInstallPath);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Execute_WindowsArm64_DoesNotFallBackOnHttpFailure(bool releaseLookupSucceeds)
    {
        const string version = "v3.4.17";
        var releasePath = $"/repos/tailwindlabs/tailwindcss/releases/tags/{version}";
        var requestedPaths = new List<string>();
        var task = PrepareWindowsArm64(version, path =>
        {
            requestedPaths.Add(path);
            return path == releasePath && releaseLookupSucceeds
                ? CreateRelease(version, hasNativeAsset: true)
                : new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        try
        {
            SeedCache(task, version, WindowsX64, "cached x64");

            task.Execute().Should().BeFalse();

            task.StandaloneCliPath.Should().BeEmpty();
            requestedPaths.Should().Equal(releaseLookupSucceeds ? [releasePath, $"/{WindowsArm64}"] : [releasePath]);
            task.Log.HasLoggedErrors.Should().BeTrue();
        }
        finally
        {
            DeleteDirectory(task.RootInstallPath);
        }
    }

    [Fact]
    public void Execute_WindowsArm64_UsesPinnedNativeCacheWithoutReleaseLookup()
    {
        const string version = "v3.4.17";
        var task = PrepareWindowsArm64(version, _ => throw new InvalidOperationException("Unexpected release lookup."));

        try
        {
            SeedCache(task, version, WindowsArm64, "cached ARM64");

            task.Execute().Should().BeTrue();

            task.StandaloneCliPath.Should().Be(task.GetFilePath());
            File.ReadAllText(task.StandaloneCliPath).Should().Be("cached ARM64");
        }
        finally
        {
            DeleteDirectory(task.RootInstallPath);
        }
    }

    [Fact]
    public void Execute_WindowsArm64_LatestCacheFollowsTheResolvedTag()
    {
        const string releasePath = "/repos/tailwindlabs/tailwindcss/releases/latest";
        var tag = "v4.3.5";
        var hasNativeAsset = false;
        var requestedPaths = new List<string>();
        var task = PrepareWindowsArm64("latest", path =>
        {
            requestedPaths.Add(path);
            return path == releasePath
                ? CreateRelease(tag, hasNativeAsset)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(tag) };
        });

        try
        {
            SeedCache(task, "latest", WindowsX64, "legacy x64");

            task.Execute().Should().BeTrue();
            task.StandaloneCliPath.Should().Be(GetCachePath(task, tag, WindowsX64));
            File.ReadAllText(task.StandaloneCliPath).Should().Be(tag);
            task.Execute().Should().BeTrue();

            tag = "v4.3.6";
            hasNativeAsset = true;
            task.Execute().Should().BeTrue();

            task.StandaloneCliPath.Should().Be(GetCachePath(task, tag, WindowsArm64));
            File.ReadAllText(task.StandaloneCliPath).Should().Be(tag);
            requestedPaths.Should().Equal(releasePath, $"/{WindowsX64}", releasePath, releasePath, $"/{WindowsArm64}");
        }
        finally
        {
            DeleteDirectory(task.RootInstallPath);
        }
    }

    private GetTailwindCLI PrepareWindowsArm64(string version, Func<string, HttpResponseMessage> respond)
    {
        return this.fixture.Prepare(task =>
        {
            task.Version = version;
            task.RootInstallPath = Path.Combine(Path.GetTempPath(), $"tailwind-cli-{Guid.NewGuid():N}");
            task.GetPreferredFileName = () => WindowsArm64;
            task.CreateDownloader = () => new TailwindDownloader(new HttpClient(new ReleaseHandler(respond)));
        });
    }

    private static HttpResponseMessage CreateRelease(string tag, bool hasNativeAsset)
    {
        // Put x64 first to ensure selection follows architecture preference rather than API order.
        string[] files = hasNativeAsset ? [WindowsX64, WindowsArm64] : [WindowsX64];
        var release = JsonSerializer.Serialize(new
        {
            tag_name = tag,
            assets = files.Select(file => new { name = file, browser_download_url = $"https://example.test/{file}" })
        });
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(release) };
    }

    private static string GetCachePath(GetTailwindCLI task, string version, string fileName)
        => Path.Combine(task.RootInstallPath, version, fileName);

    private static void SeedCache(GetTailwindCLI task, string version, string fileName, string content)
    {
        var path = GetCachePath(task, version, fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static void DeleteDirectory(string path)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, recursive: true);

                return;
            }
            catch (IOException) when (attempt < 4)
            {
                Thread.Sleep(250);
            }
            catch (UnauthorizedAccessException) when (attempt < 4)
            {
                Thread.Sleep(250);
            }
        }
    }

    private sealed class ReleaseHandler(Func<string, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(respond(request.RequestUri!.AbsolutePath));
    }
}
