namespace Tailwind.MSBuild.IntegrationTests;

using FluentAssertions;
using Tailwind.MSBuild.Tasks;
using Xunit;

public sealed class SampleAppIntegrationTests : IDisposable
{
    private readonly List<string> tempDirectories = [];

    [Fact]
    public void BuildSampleApp_UsesMsBuildTasksAndGeneratesDefaultInputFile()
    {
        var context = CreateIsolatedSampleApp();
        const string version = "test-version";

        SeedCachedTailwindBinary(context.CliCacheRoot, version);

        var result = RunProcess(
            "dotnet",
            BuildSampleArgs(context.SampleProjectPath, context.TaskAssemblyPath, context.CliCacheRoot, version),
            context.SampleDirectory);

        result.ExitCode.Should().Be(
            0,
            "sample build should succeed with Tailwind.MSBuild tasks enabled. StdOut: {0}{1}StdErr: {2}",
            Environment.NewLine,
            result.StandardOutput,
            result.StandardError);

        var inputCssPath = Path.Combine(context.SampleDirectory, "Properties", "tailwind.input.css");
        File.Exists(inputCssPath).Should().BeTrue();

        var inputCss = File.ReadAllText(inputCssPath);
        inputCss.Should().Contain("@import \"tailwindcss\";");
        inputCss.Should().Contain("@source \"../\";");

        var lockFilePath = Path.Combine(context.SampleDirectory, "obj", "tailwind-cli", "watch.lock");
        File.Exists(lockFilePath).Should().BeFalse();
    }

    public void Dispose()
    {
        foreach (var directory in this.tempDirectories.OrderByDescending(_ => _.Length))
        {
            try
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private SampleContext CreateIsolatedSampleApp()
    {
        var repositoryRoot = FindRepositoryRoot();
        var tempRoot = Path.Combine(Path.GetTempPath(), "tailwind-msbuild-integration", Guid.NewGuid().ToString("N"));
        var isolatedRoot = Path.Combine(tempRoot, "repo");
        var sampleSource = Path.Combine(repositoryRoot, "Sample", "SampleApp");
        var sampleDestination = Path.Combine(isolatedRoot, "Sample", "SampleApp");
        var buildSource = Path.Combine(repositoryRoot, "Source", "Tailwind.MSBuild", "build");
        var buildDestination = Path.Combine(isolatedRoot, "Source", "Tailwind.MSBuild", "build");
        var cliCacheRoot = Path.Combine(tempRoot, "cli-cache");

        CopyDirectory(sampleSource, sampleDestination);
        CopyDirectory(buildSource, buildDestination);

        this.tempDirectories.Add(tempRoot);

        return new SampleContext(
            SampleDirectory: sampleDestination,
            SampleProjectPath: Path.Combine(sampleDestination, "SampleApp.csproj"),
            TaskAssemblyPath: typeof(GetTailwindCLI).Assembly.Location,
            CliCacheRoot: cliCacheRoot);
    }

    private static string BuildSampleArgs(string sampleProjectPath, string taskAssemblyPath, string cliCacheRoot, string version)
    {
        return string.Join(
            " ",
            [
                "build",
                Quote(sampleProjectPath),
                "-nologo",
                "-v",
                "minimal",
                $"-p:TailwindMSBuildAssembly={Quote(taskAssemblyPath)}",
                $"-p:TailwindInstallPath={Quote(cliCacheRoot)}",
                $"-p:TailwindVersion={version}",
                "-p:TailwindWatch=false",
                "-p:ImplicitUsings=enable"
            ]);
    }

    private static CommandResult RunProcess(string fileName, string arguments, string workingDirectory)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };

        var standardOutput = new StringBuilder();
        var standardError = new StringBuilder();

        process.OutputDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data != null)
                standardOutput.AppendLine(eventArgs.Data);
        };

        process.ErrorDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data != null)
                standardError.AppendLine(eventArgs.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        const int timeoutMs = 180000;
        if (!process.WaitForExit(timeoutMs))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"Command timed out after {timeoutMs}ms: {fileName} {arguments}");
        }

        return new CommandResult(process.ExitCode, standardOutput.ToString(), standardError.ToString());
    }

    private static void SeedCachedTailwindBinary(string cliCacheRoot, string version)
    {
        var getTailwind = new GetTailwindCLI
        {
            Version = version,
            RootInstallPath = cliCacheRoot
        };

        var expectedCliPath = getTailwind.GetFilePath();
        Directory.CreateDirectory(Path.GetDirectoryName(expectedCliPath)!);
        File.Copy(ResolveDotnetHostPath(), expectedCliPath, overwrite: true);

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            MarkExecutable(expectedCliPath);
    }

    private static string ResolveDotnetHostPath()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        if (!string.IsNullOrWhiteSpace(fromEnvironment) && File.Exists(fromEnvironment))
            return fromEnvironment;

        var executableName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "dotnet.exe" : "dotnet";
        var pathEnvironment = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var pathSeparators = pathEnvironment.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

        foreach (var directory in pathSeparators)
        {
            var candidate = Path.Combine(directory.Trim(), executableName);
            if (File.Exists(candidate))
                return candidate;
        }

        throw new FileNotFoundException("Unable to locate the dotnet host path from DOTNET_HOST_PATH or PATH.");
    }

    private static void MarkExecutable(string path)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "chmod",
                Arguments = $"+x \"{path}\"",
                UseShellExecute = false,
                RedirectStandardError = true
            }
        };

        process.Start();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"Unable to mark '{path}' as executable: {error}");
    }

    private static void CopyDirectory(string sourcePath, string destinationPath)
    {
        var sourceDirectory = new DirectoryInfo(sourcePath);

        if (!sourceDirectory.Exists)
            throw new DirectoryNotFoundException($"Source directory does not exist: {sourcePath}");

        Directory.CreateDirectory(destinationPath);

        foreach (var directory in sourceDirectory.GetDirectories("*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourcePath, directory.FullName);
            Directory.CreateDirectory(Path.Combine(destinationPath, relative));
        }

        foreach (var file in sourceDirectory.GetFiles("*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourcePath, file.FullName);
            var destinationFile = Path.Combine(destinationPath, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationFile)!);
            file.CopyTo(destinationFile, overwrite: true);
        }
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current != null)
        {
            var solutionPath = Path.Combine(current.FullName, "Tailwind.MSBuild.sln");

            if (File.Exists(solutionPath))
                return current.FullName;

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate repository root from test runtime directory.");
    }

    private static string Quote(string value)
    {
        return $"\"{value}\"";
    }

    private sealed record SampleContext(
        string SampleDirectory,
        string SampleProjectPath,
        string TaskAssemblyPath,
        string CliCacheRoot);

    private sealed record CommandResult(
        int ExitCode,
        string StandardOutput,
        string StandardError);
}
