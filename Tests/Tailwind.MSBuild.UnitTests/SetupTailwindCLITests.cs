namespace Tailwind.MSBuild.Tests;

using Xunit;
using Tailwind.MSBuild.Tests.Common;
using Tailwind.MSBuild.Tasks;

public class SetupTailwindCLITests : IClassFixture<TaskFixture<SetupTailwindCLI>>
{
    private readonly TaskFixture<SetupTailwindCLI> fixture;

    public SetupTailwindCLITests(TaskFixture<SetupTailwindCLI> fixture)
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
