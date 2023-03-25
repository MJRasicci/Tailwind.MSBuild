namespace Tailwind.MSBuild.Tests;

using Xunit;
using Tailwind.MSBuild.Tests.Common;
using Tailwind.MSBuild.Tasks;
using FluentAssertions;
using System.Diagnostics;

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
