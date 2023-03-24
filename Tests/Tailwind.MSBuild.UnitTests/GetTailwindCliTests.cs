namespace Tailwind.MSBuild.Tests;

using Xunit;
using Tailwind.MSBuild.Tests.Common;
using Tailwind.MSBuild.Tasks;
using FluentAssertions;

public class GetTailwindCliTests : IClassFixture<TaskFixture<GetTailwindCLI>>
{
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

        Directory.Delete(getTailwindCli.RootInstallPath, true);
    }

    [Fact]
    public void GetTailwindCli_UsesCache()
    {
        var getTailwindCli = this.fixture.Prepare(options =>
        {
            options.Version = "latest";
            options.RootInstallPath = $"./{Guid.NewGuid()}/";
        });

        var filePath = getTailwindCli.GetFilePath();

        Directory.CreateDirectory(Path.GetDirectoryName(filePath));

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

        Directory.Delete(getTailwindCli.RootInstallPath, true);
    }
}
