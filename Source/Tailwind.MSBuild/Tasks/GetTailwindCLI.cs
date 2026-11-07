namespace Tailwind.MSBuild.Tasks;

using Tailwind.MSBuild.Utilities;

// Prevent conflicts with System.Reflection.ProcessorArchitecture
using ProcessorArchitecture = Microsoft.Build.Utilities.ProcessorArchitecture;

/// <summary>
///		An MSBuild task to verify the tailwindcss standalone-cli tool is available on the current machine. Outputs the absolute path to the executable.
/// </summary>
public class GetTailwindCLI : Microsoft.Build.Utilities.Task
{
    private const string WindowsArm64FileName = "tailwindcss-windows-arm64.exe";
    private const string WindowsX64FileName = "tailwindcss-windows-x64.exe";

    internal Func<string> GetPreferredFileName { get; set; } = GetPlatformFileName;
    internal Func<TailwindDownloader> CreateDownloader { get; set; } = () => new TailwindDownloader();

	/// <summary>
	///		The version tag of the tailwind release to use, defaults to "latest" for the most current release.
	/// </summary>
	[Required]
    public string Version { get; set; } = string.Empty;

    /// <summary>
    ///		The directory where the executable should be located.
    /// </summary>
    [Required]
    public string RootInstallPath { get; set; } = string.Empty;

    /// <summary>
    ///		The full path to the executable, set by the task. This is returned to the build engine to use as an input for other tasks.
    /// </summary>
    [Output]
    public string StandaloneCliPath { get; set; } = string.Empty;

    /// <summary>
    ///		Checks to see if the standalone-cli tool is available at the expected install path, otherwise downloads it from github.
    /// </summary>
    public override bool Execute()
    {
        this.StandaloneCliPath = string.Empty;

        try
        {
            var preferredFileName = this.GetPreferredFileName();
            var preferredPath = GetFilePath(this.Version, preferredFileName);

            // On Windows ARM64, resolve the moving alias to a concrete release before
            // using the cache. An old x64 entry under "latest" may be from another tag.
            var resolveLatest = preferredFileName == WindowsArm64FileName && this.Version == "latest";
            if (!resolveLatest && UseCachedCli(preferredPath))
                this.StandaloneCliPath = preferredPath;
            else
                this.StandaloneCliPath = ResolveCli(preferredFileName);
        }
        catch (Exception ex)
        {
            var projectFile = this.BuildEngine.ProjectFileOfTaskNode;
            var projectDirectory = string.IsNullOrWhiteSpace(projectFile) ? string.Empty : Path.GetDirectoryName(projectFile);
            var targetsFile = string.IsNullOrWhiteSpace(projectDirectory)
                ? this.BuildEngine.ProjectFileOfTaskNode
                : Path.Combine(projectDirectory, "build", "Tailwind.MSBuild.targets");

            this.Log.LogErrorFromException(ex, true, true, targetsFile);
        }

        return !this.Log.HasLoggedErrors;
    }

    /// <summary>
    ///     Gets the preferred path for the configured version. Use <see cref="StandaloneCliPath"/>
    ///     after execution for the selected asset, including when Windows ARM64 resolves "latest".
    /// </summary>
    public string GetFilePath() => GetFilePath(this.Version, this.GetPreferredFileName());

    private string GetFilePath(string version, string fileName)
        => Path.GetFullPath(Path.Combine(this.RootInstallPath, version, fileName));

    private static string GetPlatformFileName()
    {
        // The file name is specific to the current platform and architecture
        var platform = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "macos"
                     : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "linux"
                     : RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows"
                     : throw new PlatformNotSupportedException("The TailwindCSS Standalone CLI does not support the current OS");

        var arch = ProcessorArchitecture.CurrentProcessArchitecture;

        if (platform == "windows" && (arch == ProcessorArchitecture.ARM64 || RuntimeInformation.OSArchitecture == Architecture.Arm64))
            return WindowsArm64FileName;

        if (arch == ProcessorArchitecture.AMD64)
            arch = "x64";
        else
            arch = arch.ToLower(CultureInfo.CurrentCulture);

        var fileName = $"tailwindcss-{platform}-{arch}";

        if (platform == "windows")
            fileName = $"{fileName}.exe";

        return fileName;
    }

    private string ResolveCli(string preferredFileName)
    {
        using var client = this.CreateDownloader();

        var release = client.GetReleaseAsync(this.Version).GetAwaiter().GetResult()
                   ?? throw new InvalidOperationException($"Unable to find Tailwind release '{this.Version}'.");

        var asset = FindAsset(release, preferredFileName);
        var fileName = preferredFileName;
        if (asset == null && preferredFileName == WindowsArm64FileName)
        {
            asset = FindAsset(release, WindowsX64FileName);
            if (asset != null)
            {
                fileName = WindowsX64FileName;
                this.Log.LogMessage(MessageImportance.Low, "Tailwind release '{0}' has no Windows ARM64 executable. Using Windows x64.", release.TagName);
            }
        }

        if (asset == null)
        {
            var expected = preferredFileName == WindowsArm64FileName
                ? $"'{WindowsArm64FileName}' or '{WindowsX64FileName}'"
                : $"'{preferredFileName}'";
            throw new InvalidOperationException($"Unable to find a download link for {expected} with Tailwind version '{this.Version}'.");
        }

        var version = preferredFileName == WindowsArm64FileName && this.Version == "latest"
            ? GetReleaseCacheVersion(release.TagName)
            : this.Version;
        var path = GetFilePath(version, fileName);

        if (!UseCachedCli(path))
            DownloadCli(client, asset, path);

        return path;
    }

    private static GitHubReleaseAsset? FindAsset(GitHubReleaseResponse release, string fileName)
        => release.Assets.FirstOrDefault(asset => string.Equals(asset.Name, fileName, StringComparison.OrdinalIgnoreCase));

    private static string GetReleaseCacheVersion(string tagName)
    {
        if (string.IsNullOrWhiteSpace(tagName) || tagName is "." or ".." ||
            tagName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || tagName.Contains('/') || tagName.Contains('\\'))
        {
            throw new InvalidOperationException($"GitHub returned an invalid cache tag for the latest Tailwind release: '{tagName}'.");
        }

        return tagName;
    }

    private bool UseCachedCli(string path)
    {
        if (!File.Exists(path))
            return false;

        this.Log.LogMessage(MessageImportance.Low, "Using cached tailwind binary '{0}'", path);
        return true;
    }

    private void DownloadCli(TailwindDownloader client, GitHubReleaseAsset asset, string path)
    {
        this.Log.LogMessage(MessageImportance.Low, "Downloading '{0}'", asset.DownloadUrl);
        var response = client.GetAssetAsync(asset).GetAwaiter().GetResult();

        this.Log.LogMessage(MessageImportance.Low, "Writing file to '{0}'", path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllBytes(temporaryPath, response);
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                SetPosixFilePermissions(temporaryPath);

            try
            {
                File.Move(temporaryPath, path);
            }
            catch (IOException) when (File.Exists(path))
            {
                // Another build published the same release asset while we downloaded it.
            }
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    private void SetPosixFilePermissions(string path)
    {
        // Grant execute permissions to current user
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "chmod",
                Arguments = $"+x \"{path.Replace("\"", "\\\"")}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
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

        process.Start();
        process.BeginErrorReadLine();
        process.BeginOutputReadLine();
        process.WaitForExit();

        if (process.ExitCode != 0)
            throw new IOException($"Unable to make Tailwind CLI executable at '{path}'. chmod exited with code {process.ExitCode}.");
    }
}
