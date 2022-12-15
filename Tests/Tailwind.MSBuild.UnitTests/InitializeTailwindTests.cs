namespace Tailwind.MSBuild.Tests;

using Moq;
using Tailwind.MSBuild.Tests.Common;
using Tasks;
using Xunit;

public class InitializeTailwindTests : IClassFixture<TaskFixture<InitializeTailwind>>
{
    private readonly TaskFixture<InitializeTailwind> fixture;

    public InitializeTailwindTests(TaskFixture<InitializeTailwind> fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public void Execute_LatestSucceeds()
    {
        var task = this.fixture.Prepare(t =>
        {
            t.Version = "latest";
            t.RootInstallPath = Directory.GetCurrentDirectory();
        });
    }

    [Fact]
    public void Execute_VersionLockSucceeds()
    {
        var task = this.fixture.Prepare(t =>
        {
            t.Version = "v3.2.4";
            t.RootInstallPath = Directory.GetCurrentDirectory();
        });
    }

    [Fact]
    public void Execute_UsesCache()
    {
        var task = this.fixture.Prepare(t =>
        {
            t.Version = "v3.2.4";
            t.RootInstallPath = Directory.GetCurrentDirectory();
        });
    }

    [Fact]
    public void Execute_LogsExceptions() { }
}
