namespace Tailwind.MSBuild.Tests;

using Xunit;
using Tailwind.MSBuild.Tests.Common;
using Tailwind.MSBuild.Tasks;
using FluentAssertions;
using System.Runtime.InteropServices;
using System.Threading;

using ProcessorArchitecture = Microsoft.Build.Utilities.ProcessorArchitecture;

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

        DeleteDirectory(getTailwindCli.RootInstallPath);
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

    [Fact]
    public void GetFilePath_WindowsArm64_FallsBackToX64Binary()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || ProcessorArchitecture.CurrentProcessArchitecture != ProcessorArchitecture.ARM64)
            return;

        var getTailwindCli = this.fixture.Prepare(options =>
        {
            options.Version = "latest";
            options.RootInstallPath = $"./{Guid.NewGuid()}/";
        });

        var filePath = getTailwindCli.GetFilePath();

        filePath.Should().EndWith("tailwindcss-windows-x64.exe");
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
}
