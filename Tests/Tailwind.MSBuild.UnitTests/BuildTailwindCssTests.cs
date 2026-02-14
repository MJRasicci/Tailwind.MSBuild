namespace Tailwind.MSBuild.Tests;

using FluentAssertions;
using Tailwind.MSBuild.Tasks;
using Tailwind.MSBuild.Tests.Common;
using Tailwind.MSBuild.Utilities;
using Xunit;

public sealed class BuildTailwindCssTests : IDisposable
{
    private readonly HashSet<int> watchProcessIds = [];
    private readonly List<string> tempDirectories = [];

    [Fact]
    public void WatchMode_FirstRun_WritesLockFileEntry()
    {
        var paths = CreatePaths();
        var task = CreateTask(paths, watch: true);

        task.Execute().Should().BeTrue();

        var lockFile = ReadLockFile(paths.LockFilePath, out var isCorrupt);
        isCorrupt.Should().BeFalse();
        lockFile.Entries.Should().ContainSingle();

        var entry = lockFile.Entries.Single();
        entry.ProjectDirectory.Should().Be(paths.ProjectDirectory);
        entry.ProcessId.Should().BeGreaterThan(0);
        IsProcessRunning(entry.ProcessId).Should().BeTrue();

        this.watchProcessIds.Add(entry.ProcessId);
    }

    [Fact]
    public void WatchMode_DoesNotStartSecondProcessForSameProject()
    {
        var paths = CreatePaths();
        var firstTask = CreateTask(paths, watch: true);
        var secondTask = CreateTask(paths, watch: true);

        firstTask.Execute().Should().BeTrue();

        var firstLockFile = ReadLockFile(paths.LockFilePath, out _);
        var firstProcessId = firstLockFile.Entries.Single().ProcessId;

        secondTask.Execute().Should().BeTrue();

        var secondLockFile = ReadLockFile(paths.LockFilePath, out _);
        secondLockFile.Entries.Should().ContainSingle();
        secondLockFile.Entries.Single().ProcessId.Should().Be(firstProcessId);

        this.watchProcessIds.Add(firstProcessId);
    }

    [Fact]
    public void WatchMode_ReplacesStaleProcessEntries()
    {
        var paths = CreatePaths();
        const int staleProcessId = int.MaxValue;

        WriteLockFile(
            paths.LockFilePath,
            [
                new TailwindLockFileEntry
                {
                    ProcessId = staleProcessId,
                    ProjectDirectory = paths.ProjectDirectory,
                    StartedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10)
                }
            ]);

        var task = CreateTask(paths, watch: true);
        task.Execute().Should().BeTrue();

        var lockFile = ReadLockFile(paths.LockFilePath, out _);
        lockFile.Entries.Should().ContainSingle();

        var entry = lockFile.Entries.Single();
        entry.ProcessId.Should().NotBe(staleProcessId);
        entry.ProcessId.Should().BeGreaterThan(0);
        IsProcessRunning(entry.ProcessId).Should().BeTrue();

        this.watchProcessIds.Add(entry.ProcessId);
    }

    [Fact]
    public void WatchMode_ResetsCorruptLockFileAndStartsProcess()
    {
        var paths = CreatePaths();
        Directory.CreateDirectory(Path.GetDirectoryName(paths.LockFilePath)!);
        File.WriteAllText(paths.LockFilePath, "{not-valid-json", Encoding.ASCII);

        var task = CreateTask(paths, watch: true);
        task.Execute().Should().BeTrue();

        var lockFile = ReadLockFile(paths.LockFilePath, out var isCorrupt);
        isCorrupt.Should().BeFalse();
        lockFile.Entries.Should().ContainSingle();
        lockFile.Entries.Single().ProjectDirectory.Should().Be(paths.ProjectDirectory);

        this.watchProcessIds.Add(lockFile.Entries.Single().ProcessId);
    }

    [Fact]
    public void Execute_CreatesMissingConfigDirectoryAndDefaultInputFile()
    {
        var paths = CreatePaths(createConfigDirectory: false);
        Directory.Exists(paths.ConfigDirectory).Should().BeFalse();

        var task = CreateTask(paths, watch: false);
        task.Execute().Should().BeTrue();

        var inputPath = Path.Combine(paths.ConfigDirectory, "tailwind.input.css");
        File.Exists(inputPath).Should().BeTrue();

        var inputContents = File.ReadAllText(inputPath);
        inputContents.Should().Contain("@import \"tailwindcss\";");
        inputContents.Should().Contain("@source \"../\";");
    }

    public void Dispose()
    {
        foreach (var processId in this.watchProcessIds)
            KillProcess(processId);

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

    private static BuildTailwindCSS CreateTask(TestPaths paths, bool watch)
    {
        var fixture = new TaskFixture<BuildTailwindCSS>();

        return fixture.Prepare(task =>
        {
            task.StandaloneCliPath = paths.CliPath;
            task.ConfigDir = paths.ConfigDirectory;
            task.InputFile = "tailwind.input.css";
            task.OutputFile = paths.OutputFile;
            task.Minify = false;
            task.Watch = watch;
            task.WatchLockFile = paths.LockFilePath;
            task.ProjectDirectory = paths.ProjectDirectory;
        });
    }

    private TestPaths CreatePaths(bool createConfigDirectory = true)
    {
        var root = Path.Combine(Path.GetTempPath(), "tailwind-msbuild-tests", Guid.NewGuid().ToString("N"));
        var projectDirectory = Path.Combine(root, "Project");
        var configDirectory = Path.Combine(projectDirectory, "Tailwind");
        var lockFilePath = Path.Combine(root, "obj", "tailwind-cli", "watch.lock");
        var outputFile = Path.Combine(root, "wwwroot", "css", "tailwind.css");
        var cliDirectory = Path.Combine(root, "tools");

        Directory.CreateDirectory(projectDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(outputFile)!);

        if (createConfigDirectory)
            Directory.CreateDirectory(configDirectory);

        var cliPath = FakeTailwindCli.Create(cliDirectory);

        this.tempDirectories.Add(root);

        return new TestPaths(projectDirectory, configDirectory, lockFilePath, outputFile, cliPath);
    }

    private static TailwindLockFile ReadLockFile(string lockFilePath, out bool isCorrupt)
    {
        using var stream = new FileStream(lockFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
        return TailwindLockFile.Read(stream, out isCorrupt);
    }

    private static void WriteLockFile(string lockFilePath, IEnumerable<TailwindLockFileEntry> entries)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(lockFilePath)!);

        using var stream = new FileStream(lockFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
        new TailwindLockFile(entries).Write(stream);
    }

    private static bool IsProcessRunning(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static void KillProcess(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);

            if (process.HasExited)
                return;

            process.Kill(entireProcessTree: true);
            process.WaitForExit(5000);
        }
        catch (ArgumentException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    private sealed record TestPaths(
        string ProjectDirectory,
        string ConfigDirectory,
        string LockFilePath,
        string OutputFile,
        string CliPath);
}
