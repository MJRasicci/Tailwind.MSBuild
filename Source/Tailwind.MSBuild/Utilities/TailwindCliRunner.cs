namespace Tailwind.MSBuild.Utilities;

using System.ComponentModel;
using System.Globalization;
using System.Text;

internal sealed class TailwindCliRunner
{
    private readonly TailwindTerminalResolver terminalResolver;

    public TailwindCliRunner()
        : this(new TailwindTerminalResolver())
    {
    }

    internal TailwindCliRunner(TailwindTerminalResolver terminalResolver)
    {
        this.terminalResolver = terminalResolver;
    }

    public bool Run(TailwindCliInvocation invocation, TaskLoggingHelper log)
    {
        if (!invocation.Watch)
            return RunOnce(invocation, log);

        if (string.IsNullOrWhiteSpace(invocation.WatchLockFile))
        {
            if (!RunOnce(invocation, log))
                return false;

            StartWatchProcess(invocation, log);
            return true;
        }

        return RunWatchWithLock(invocation, log);
    }

    private static bool RunOnce(TailwindCliInvocation invocation, TaskLoggingHelper log)
    {
        using var process = new Process
        {
            StartInfo = CreateCliStartInfo(invocation, invocation.Arguments, watch: false)
        };

        var errorOutput = new StringBuilder();
        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
                errorOutput.AppendLine(e.Data);
        };

        process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
                log.LogMessage(e.Data);
        };

        log.LogCommandLine($"{invocation.ExecutablePath} {process.StartInfo.Arguments}");

        if (!TryStartProcess(process, log))
            return false;

        process.BeginErrorReadLine();
        process.BeginOutputReadLine();
        process.WaitForExit();

        var diagnostics = errorOutput.ToString().TrimEnd();
        if (process.ExitCode != 0)
        {
            log.LogError("Tailwind CLI exited with code {0}.{1}", process.ExitCode,
                diagnostics.Length == 0 ? string.Empty : Environment.NewLine + diagnostics);
            return false;
        }

        if (diagnostics.Length > 0)
            log.LogMessage(diagnostics);

        if (!File.Exists(invocation.OutputFile))
        {
            log.LogError("Tailwind CLI did not generate '{0}'.", invocation.OutputFile);
            return false;
        }

        return true;
    }

    private bool RunWatchWithLock(TailwindCliInvocation invocation, TaskLoggingHelper log)
    {
        var lockFilePath = GetLockFilePath(invocation);
        Directory.CreateDirectory(Path.GetDirectoryName(lockFilePath)!);

        using var lockStream = TryOpenLockFileStream(lockFilePath);
        if (lockStream == null)
        {
            log.LogError("Unable to access Tailwind watch lock '{0}'.", lockFilePath);
            return false;
        }

        var lockFile = TailwindLockFile.Read(lockStream, out var isCorrupt);
        if (isCorrupt)
            log.LogMessage(MessageImportance.Low, "Tailwind lock file '{0}' was invalid and has been reset.", lockFilePath);

        var pathComparison = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        bool MatchesProject(TailwindLockFileEntry entry) => string.Equals(entry.ProjectDirectory, invocation.ProjectDirectory, pathComparison);
        var runningEntry = lockFile.Entries.FirstOrDefault(entry => MatchesProject(entry) && IsSameProcessRunning(entry));

        if (runningEntry != null)
        {
            log.LogMessage(MessageImportance.Low, "Tailwind watch already running for '{0}' (PID {1}).", invocation.ProjectDirectory, runningEntry.ProcessId);
            return File.Exists(invocation.OutputFile) || RunOnce(invocation, log);
        }

        // A background watcher cannot guarantee CSS exists when MSBuild discovers static assets.
        if (!RunOnce(invocation, log))
            return false;

        lockFile.Entries.RemoveAll(entry => MatchesProject(entry));
        var watchEntry = StartWatchProcess(invocation, log);
        if (watchEntry != null)
            lockFile.Entries.Add(watchEntry);

        lockFile.Write(lockStream);
        return true;
    }

    private TailwindLockFileEntry? StartWatchProcess(TailwindCliInvocation invocation, TaskLoggingHelper log)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var watchArguments = invocation.Arguments.Concat(["--watch=always"]);
            var command = string.Join(" ", new[] { invocation.ExecutablePath }.Concat(watchArguments).Select(QuoteForShell));
            var terminalWatch = TryStartLinuxWatchTerminal(invocation, command, log);

            return terminalWatch ?? StartLinuxWatchWithoutTerminal(invocation, command, log);
        }

        using var process = new Process
        {
            StartInfo = CreateCliStartInfo(invocation, invocation.Arguments.Concat(["--watch"]), watch: true)
        };

        return TryStartProcess(process, log, watch: true) ? GetWatchEntry(invocation, process.Id) : null;
    }

    private TailwindLockFileEntry? TryStartLinuxWatchTerminal(TailwindCliInvocation invocation, string command, TaskLoggingHelper log)
    {
        if (string.IsNullOrWhiteSpace(invocation.WatchTerminalExecutable) &&
            string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY")) &&
            string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")))
        {
            return null;
        }

        var terminals = this.terminalResolver.ResolveCandidates(invocation.WatchTerminalExecutable, out var resolutionMessage);
        if (!string.IsNullOrWhiteSpace(resolutionMessage))
            log.LogWarning(resolutionMessage);

        var timer = Stopwatch.StartNew();
        foreach (var terminal in terminals)
        {
            var timeout = TimeSpan.FromSeconds(Math.Min(3, 6 - timer.Elapsed.TotalSeconds));
            if (timeout <= TimeSpan.Zero)
                break;

            var entry = StartLinuxWatchTerminal(invocation, command, terminal, timeout, log);
            if (entry != null)
                return entry;
        }

        return null;
    }

    private static TailwindLockFileEntry? StartLinuxWatchTerminal(
        TailwindCliInvocation invocation, string command, TailwindTerminalCommand terminal, TimeSpan timeout, TaskLoggingHelper log)
    {
        // Desktop terminal clients may hand off the command and exit. The shell records
        // its own PID immediately before exec, so the lock identifies the CLI itself.
        var launchDirectory = Path.Combine(Path.GetTempPath(), $"tailwind-watch-{Guid.NewGuid():N}");
        var claimDirectory = Path.Combine(launchDirectory, "claim");
        Directory.CreateDirectory(claimDirectory);
        try
        {
            // Some launchers expand shell metacharacters in command-line arguments.
            var scriptPath = Path.Combine(launchDirectory, "watch.sh");
            File.WriteAllText(scriptPath,
                $"cd {QuoteForShell(invocation.WorkingDirectory)} && mkdir {QuoteForShell(claimDirectory)}/\"$$\" && exec {command}\n");
            var terminalCommand = string.Join(" ",
                new[] { terminal.ExecutablePath }.Concat(terminal.BuildArguments(scriptPath, invocation.WorkingDirectory)).Select(QuoteForShell));
            using var process = new Process
            {
                StartInfo = CreateShellStartInfo(invocation, $"exec {terminalCommand} </dev/null >/dev/null 2>&1")
            };

            if (!TryStartProcess(process, log, watch: true))
                return null;

            var timer = Stopwatch.StartNew();
            while (timer.Elapsed < timeout)
            {
                var entry = ReadWatchEntry(invocation, claimDirectory);
                if (entry != null)
                {
                    log.LogMessage(MessageImportance.Low, "Tailwind watch started in '{0}' (PID {1}).", terminal.ExecutablePath, entry.ProcessId);
                    return entry;
                }

                if (process.HasExited && process.ExitCode != 0)
                    break;

                System.Threading.Thread.Sleep(25);
            }

            // Deleting the empty directory prevents a late terminal from starting a
            // second watcher after fallback. A concurrent PID claim makes deletion fail.
            try
            {
                Directory.Delete(claimDirectory);
            }
            catch (IOException)
            {
                var entry = ReadWatchEntry(invocation, claimDirectory);
                if (entry != null)
                    return entry;
            }

            log.LogMessage(MessageImportance.Normal, "Terminal '{0}' did not start Tailwind watch.", terminal.ExecutablePath);
            return null;
        }
        finally
        {
            if (Directory.Exists(launchDirectory))
                Directory.Delete(launchDirectory, recursive: true);
        }
    }

    private static TailwindLockFileEntry? StartLinuxWatchWithoutTerminal(TailwindCliInvocation invocation, string command, TaskLoggingHelper log)
    {
        var logFile = string.IsNullOrWhiteSpace(invocation.WatchLockFile)
            ? Path.Combine(Path.GetTempPath(), $"tailwind-watch-{Guid.NewGuid():N}.log")
            : GetLockFilePath(invocation) + ".log";
        try
        {
            using var stream = File.Open(logFile, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log.LogWarning("Unable to open Tailwind watch log '{0}': {1}", logFile, ex.Message);
            return null;
        }

        // Close MSBuild's inherited pipes before the CLI starts so dotnet watch can
        // finish its build. --watch=always keeps the CLI alive with stdin closed.
        using var process = new Process
        {
            StartInfo = CreateShellStartInfo(invocation, $"exec {command} </dev/null >>{QuoteForShell(logFile)} 2>&1")
        };

        if (!TryStartProcess(process, log, watch: true))
            return null;

        if (process.WaitForExit(100))
        {
            log.LogWarning("Tailwind watch exited with code {0} during startup. Output: {1}", process.ExitCode, logFile);
            return null;
        }

        log.LogMessage(MessageImportance.High, "Tailwind watch started without a terminal (PID {0}). Output: {1}", process.Id, logFile);
        return GetWatchEntry(invocation, process.Id);
    }

    private static TailwindLockFileEntry? ReadWatchEntry(TailwindCliInvocation invocation, string launchDirectory)
    {
        var processDirectory = Directory.EnumerateDirectories(launchDirectory).FirstOrDefault();
        var text = Path.GetFileName(processDirectory);
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var processId)
            ? GetWatchEntry(invocation, processId)
            : null;
    }

    private static TailwindLockFileEntry? GetWatchEntry(TailwindCliInvocation invocation, int processId)
    {
        var startedAt = GetStartTime(processId);
        return startedAt.HasValue
            ? new TailwindLockFileEntry
            {
                ProcessId = processId,
                StartedAtUtc = startedAt.Value,
                ProjectDirectory = invocation.ProjectDirectory
            }
            : null;
    }

    private static ProcessStartInfo CreateCliStartInfo(TailwindCliInvocation invocation, IEnumerable<string> arguments, bool watch)
    {
        return new ProcessStartInfo
        {
            WorkingDirectory = invocation.WorkingDirectory,
            FileName = invocation.ExecutablePath,
            Arguments = BuildProcessArguments(arguments),
            UseShellExecute = watch,
            RedirectStandardOutput = !watch,
            RedirectStandardError = !watch,
            CreateNoWindow = !watch
        };
    }

    private static ProcessStartInfo CreateShellStartInfo(TailwindCliInvocation invocation, string command)
    {
        return new ProcessStartInfo
        {
            WorkingDirectory = invocation.WorkingDirectory,
            FileName = "/bin/sh",
            Arguments = BuildProcessArguments(["-c", command]),
            UseShellExecute = false
        };
    }

    private static string BuildProcessArguments(IEnumerable<string> arguments)
    {
        return string.Join(" ", arguments.Select(QuoteProcessArgument));
    }

    private static string QuoteProcessArgument(string argument)
    {
        if (string.IsNullOrEmpty(argument))
            return "\"\"";

        if (!argument.Any(character => char.IsWhiteSpace(character) || (character is '"' or '\\')))
            return argument;

        var builder = new StringBuilder();
        builder.Append('"');

        var backslashCount = 0;

        foreach (var character in argument)
        {
            if (character == '\\')
            {
                backslashCount++;
                continue;
            }

            if (character == '"')
            {
                builder.Append('\\', (backslashCount * 2) + 1);
                builder.Append(character);
                backslashCount = 0;
                continue;
            }

            if (backslashCount > 0)
            {
                builder.Append('\\', backslashCount);
                backslashCount = 0;
            }

            builder.Append(character);
        }

        if (backslashCount > 0)
            builder.Append('\\', backslashCount * 2);

        builder.Append('"');
        return builder.ToString();
    }

    private static string QuoteForShell(string value)
    {
        return "'" + value.Replace("'", "'\"'\"'") + "'";
    }

    private static bool TryStartProcess(Process process, TaskLoggingHelper log, bool watch = false)
    {
        string message;
        try
        {
            if (process.Start())
                return true;

            message = $"Unable to start process from executable '{process.StartInfo.FileName}'.";
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            message = $"Unable to start process from executable '{process.StartInfo.FileName}': {ex.Message}";
        }

        if (watch)
            log.LogWarning(message);
        else
            log.LogError(message);

        return false;
    }

    private static FileStream? TryOpenLockFileStream(string lockFilePath)
    {
        const int maxAttempts = 150;
        const int retryDelayMs = 100;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                return new FileStream(lockFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException)
            {
                if (attempt == maxAttempts - 1)
                    break;

                System.Threading.Thread.Sleep(retryDelayMs);
            }
            catch (UnauthorizedAccessException)
            {
                break;
            }
        }

        return null;
    }

    private static string GetLockFilePath(TailwindCliInvocation invocation)
    {
        if (Path.IsPathRooted(invocation.WatchLockFile))
            return invocation.WatchLockFile;

        return Path.GetFullPath(Path.Combine(invocation.ProjectDirectory, invocation.WatchLockFile));
    }

    private static bool IsSameProcessRunning(TailwindLockFileEntry entry)
    {
        var startTime = GetStartTime(entry.ProcessId);
        // Older lock files recorded the current time immediately after starting the process.
        return startTime.HasValue && (startTime.Value - entry.StartedAtUtc).Duration() <= TimeSpan.FromSeconds(1);
    }

    private static DateTimeOffset? GetStartTime(int processId)
    {
        if (processId <= 0)
            return null;

        try
        {
            using var process = Process.GetProcessById(processId);
            return process.HasExited ? null : process.StartTime.ToUniversalTime();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception)
        {
            return null;
        }
    }
}

internal sealed class TailwindCliInvocation
{
    public string ExecutablePath { get; set; } = string.Empty;

    public string WorkingDirectory { get; set; } = string.Empty;

    public IReadOnlyList<string> Arguments { get; set; } = [];

    public string OutputFile { get; set; } = string.Empty;

    public bool Watch { get; set; }

    public string ProjectDirectory { get; set; } = string.Empty;

    public string WatchLockFile { get; set; } = string.Empty;

    public string WatchTerminalExecutable { get; set; } = string.Empty;
}
