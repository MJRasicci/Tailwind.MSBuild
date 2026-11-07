namespace Tailwind.MSBuild.Tests;

using FluentAssertions;
using System.Globalization;
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
    public void WatchMode_OnLinux_UsesConfiguredTerminalExecutable()
    {
        if (!OperatingSystem.IsLinux())
            return;

        var paths = CreatePaths();
        var terminalArgumentsPath = Path.Combine(Path.GetDirectoryName(paths.CliPath)!, "terminal.args");
        var terminalPath = FakeTerminalEmulator.Create(Path.GetDirectoryName(paths.CliPath)!, "x-terminal-emulator", terminalArgumentsPath);
        var task = CreateTask(paths, watch: true, terminalExecutable: terminalPath, terminalResolver: new TailwindTerminalResolver(pathEnvironment: string.Empty));

        task.Execute().Should().BeTrue();

        SpinWait.SpinUntil(() => File.Exists(terminalArgumentsPath), TimeSpan.FromSeconds(5)).Should().BeTrue();
        File.ReadAllText(terminalArgumentsPath).Should().Contain("-e");

        var lockFile = ReadLockFile(paths.LockFilePath, out _);
        lockFile.Entries.Should().ContainSingle();
        IsProcessRunning(lockFile.Entries.Single().ProcessId).Should().BeTrue();

        this.watchProcessIds.Add(lockFile.Entries.Single().ProcessId);
    }

    [Fact]
    public void WatchMode_OnLinux_ReusesCliAfterTerminalLauncherExits()
    {
        if (!OperatingSystem.IsLinux())
            return;

        var paths = CreatePaths();
        var toolsDirectory = Path.GetDirectoryName(paths.CliPath)!;
        var terminalArgumentsPath = Path.Combine(toolsDirectory, "terminal.args");
        var terminal = FakeTerminalEmulator.Create(toolsDirectory, "x-terminal-emulator", terminalArgumentsPath, brokered: true);
        var task = CreateTask(paths, watch: true, terminalExecutable: terminal);

        task.Execute().Should().BeTrue();
        var cliPid = ReadLockFile(paths.LockFilePath, out _).Entries.Single().ProcessId;
        this.watchProcessIds.Add(cliPid);
        var launcherPid = int.Parse(File.ReadAllText(terminalArgumentsPath + ".pid"), CultureInfo.InvariantCulture);
        cliPid.Should().NotBe(launcherPid);
        SpinWait.SpinUntil(() => !IsProcessRunning(launcherPid), TimeSpan.FromSeconds(5)).Should().BeTrue();

        CreateTask(paths, watch: true, terminalExecutable: terminal).Execute().Should().BeTrue();
        var reusedPid = ReadLockFile(paths.LockFilePath, out _).Entries.Single().ProcessId;
        this.watchProcessIds.Add(reusedPid);
        reusedPid.Should().Be(cliPid);
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

    private static BuildTailwindCSS CreateTask(
        TestPaths paths,
        bool watch,
        string? terminalExecutable = null,
        TailwindTerminalResolver? terminalResolver = null)
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
            task.TerminalExecutable = terminalExecutable ?? string.Empty;
            task.ProjectDirectory = paths.ProjectDirectory;
            task.CliRunner = new TailwindCliRunner(terminalResolver ?? new TailwindTerminalResolver(pathEnvironment: string.Empty));
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
