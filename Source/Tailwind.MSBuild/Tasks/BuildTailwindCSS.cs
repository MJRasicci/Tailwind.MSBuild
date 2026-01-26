namespace Tailwind.MSBuild.Tasks;

using Tailwind.MSBuild.Utilities;

/// <summary>
///     An MSBuild task to build TailwindCSS within a project.
/// </summary>
public class BuildTailwindCSS : Microsoft.Build.Utilities.Task
{
    /// <summary>
    ///     The value of MSBuildProjectDirectory.
    /// </summary>
    [Required]
    public string ProjectDirectory { get; set; } = string.Empty;

    /// <summary>
    ///		The full path to the standalone-cli executable.
    /// </summary>
    [Required]
	public string StandaloneCliPath { get; set; } = string.Empty;

    /// <summary>
    ///		The directory containing the tailwind configuration files.
    /// </summary>
    [Required]
    public string ConfigDir { get; set; } = string.Empty;

    /// <summary>
    ///     The name of the input css file.
    /// </summary>
    [Required]
    public string InputFile { get; set; } = string.Empty;

    /// <summary>
    ///     The path where the output css file will be located.
    /// </summary>
    [Required]
    public string OutputFile { get; set; } = string.Empty;

    /// <summary>
    ///     Whether the generated css should be minified or not.
    /// </summary>
    [Required]
    public bool Minify { get; set; }

    /// <summary>
    ///     Whether to run TailwindCSS in watch mode.
    /// </summary>
    [Required]
    public bool Watch { get; set; }

    /// <summary>
    ///     The path to the lock file used to prevent duplicate Tailwind CLI instances.
    /// </summary>
    [Required]
    public string WatchLockFile { get; set; } = string.Empty;

    /// <summary>
    ///     The absolute path to the file where the css was generated.
    /// </summary>
    [Output]
    public string GeneratedCssFile { get; set; } = string.Empty;

    /// <summary>
    ///     Builds tailwindcss for the current project.
    /// </summary>
    public override bool Execute()
	{
        this.InputFile = Path.Combine(this.ConfigDir, this.InputFile);

        if (!File.Exists(this.InputFile))
        {
            var sourcePath = GetPathToSourceDirectory();

            using var file = File.CreateText(this.InputFile);
            file.WriteLine("@import \"tailwindcss\";");

            if (!string.IsNullOrWhiteSpace(sourcePath))
                file.WriteLine($"@source \"{sourcePath}\";");

            file.Close();
        }

        // Build
        RunCli($"-i \"{this.InputFile}\" -o \"{this.OutputFile}\"{(this.Minify ? " -m" : string.Empty)}{(this.Watch ? " --watch" : string.Empty)}");

        this.GeneratedCssFile = this.OutputFile;

        return !this.Log.HasLoggedErrors;
    }

	private void RunCli(string args)
	{
        // The CLI process runs entirely in the background within the directory containing the configuration file
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                WorkingDirectory = this.ConfigDir,
                FileName = this.StandaloneCliPath,
                Arguments = args,
                UseShellExecute = this.Watch,
                RedirectStandardOutput = !this.Watch,
                RedirectStandardError = !this.Watch,
                CreateNoWindow = !this.Watch
            }
        };

        // Route stderr and stdio to our TaskLoggingHelper
        process.ErrorDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
                this.Log.LogMessage(e.Data);
        };

        process.OutputDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
                this.Log.LogMessage(e.Data);
        };

        this.Log.LogCommandLine($"{process.StartInfo.FileName} {process.StartInfo.Arguments}");

        if (ShouldUseLockFile())
        {
            if (!TryStartProcessWithLock(process))
                return;
        }
        else
        {
            if (!StartProcess(process))
                return;
        }

        if (!this.Watch)
        {
            process.BeginErrorReadLine();
            process.BeginOutputReadLine();
            process.WaitForExit();
        }
    }

    private bool TryStartProcessWithLock(Process process)
    {
        var lockFilePath = GetLockFilePath();
        var lockFileDirectory = Path.GetDirectoryName(lockFilePath);

        if (!string.IsNullOrWhiteSpace(lockFileDirectory))
            Directory.CreateDirectory(lockFileDirectory);

        using var lockStream = TryOpenLockFileStream(lockFilePath);

        if (lockStream == null)
        {
            this.Log.LogMessage(MessageImportance.Low, "Unable to access Tailwind lock file '{0}' after multiple attempts.", lockFilePath);
            return false;
        }

        var lockFile = TailwindLockFile.Read(lockStream, out var isCorrupt);

        if (isCorrupt)
            this.Log.LogMessage(MessageImportance.Low, "Tailwind lock file '{0}' was invalid and has been reset.", lockFilePath);

        var matchingEntries = lockFile.Entries
                                      .Where(entry => string.Equals(entry.ProjectDirectory, this.ProjectDirectory, StringComparison.OrdinalIgnoreCase))
                                      .ToList();

        var runningEntry = matchingEntries.FirstOrDefault(entry => IsProcessRunning(entry.ProcessId));

        if (runningEntry != null)
        {
            this.Log.LogMessage(MessageImportance.Low, "Tailwind watch already running for '{0}' (PID {1}).", this.ProjectDirectory, runningEntry.ProcessId);
            return false;
        }

        if (matchingEntries.Count > 0)
            lockFile.Entries.RemoveAll(entry => string.Equals(entry.ProjectDirectory, this.ProjectDirectory, StringComparison.OrdinalIgnoreCase));

        if (!StartProcess(process))
            return false;

        lockFile.Entries.Add(new TailwindLockFileEntry
        {
            ProcessId = process.Id,
            StartedAtUtc = DateTimeOffset.UtcNow,
            ProjectDirectory = this.ProjectDirectory
        });

        lockFile.Write(lockStream);
        return true;
    }

    private bool StartProcess(Process process)
    {
        if (process.Start())
            return true;

        this.Log.LogError($"Unable to start process from executable {process.StartInfo.FileName}");
        return false;
    }

    private bool ShouldUseLockFile()
    {
        return this.Watch && !string.IsNullOrWhiteSpace(this.WatchLockFile);
    }

    private static FileStream? TryOpenLockFileStream(string lockFilePath)
    {
        const int maxAttempts = 5;
        const int retryDelayMs = 100;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                return new FileStream(lockFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) when (attempt < maxAttempts - 1)
            {
                System.Threading.Thread.Sleep(retryDelayMs);
            }
            catch (UnauthorizedAccessException)
            {
                break;
            }
        }

        return null;
    }

    private string GetLockFilePath()
    {
        if (Path.IsPathRooted(this.WatchLockFile))
            return this.WatchLockFile;

        return Path.GetFullPath(Path.Combine(this.ProjectDirectory, this.WatchLockFile));
    }

    private static bool IsProcessRunning(int processId)
    {
        if (processId <= 0)
            return false;

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
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    private string? GetPathToSourceDirectory()
    {
        // Normalize to full paths and ensure trailing separators for directory semantics
        var sourceDir = EnsureTrailingSeparator(Path.GetFullPath(this.ConfigDir));
        var destDir = EnsureTrailingSeparator(Path.GetFullPath(this.ProjectDirectory));

        var sourceUri = new Uri(sourceDir, UriKind.Absolute);
        var destUri = new Uri(destDir, UriKind.Absolute);

        // Different volumes / roots -> return absolute destination (normalized)
        if (!string.Equals(sourceUri.Scheme, destUri.Scheme, StringComparison.OrdinalIgnoreCase))
            return NormalizeSlashes(destDir);

        var relativeUri = sourceUri.MakeRelativeUri(destUri);

        var relativePath = Uri.UnescapeDataString(relativeUri.ToString());

        // Convert URI separators to POSIX path separators
        return NormalizeSlashes(relativePath);
    }

    private static string EnsureTrailingSeparator(string path)
    {
        if (!path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) &&
            !path.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal))
        {
            path += Path.DirectorySeparatorChar;
        }

        return path;
    }

    private static string NormalizeSlashes(string path)
    {
        return path.Replace('\\', '/');
    }
}
