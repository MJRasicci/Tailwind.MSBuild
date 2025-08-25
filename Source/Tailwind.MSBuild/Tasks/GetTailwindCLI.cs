namespace Tailwind.MSBuild.Tasks;

using Tailwind.MSBuild.Utilities;

/// <summary>
///		An MSBuild task to verify the tailwindcss standalone-cli tool is available on the current machine. Outputs the absolute path to the executable.
/// </summary>
public class GetTailwindCLI : Microsoft.Build.Utilities.Task
{
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
        try
        {
            this.StandaloneCliPath = GetFilePath();

            if (File.Exists(this.StandaloneCliPath))
                this.Log.LogMessage(MessageImportance.Low, "Using cached tailwind binary '{0}'", this.StandaloneCliPath);
            else
                DownloadCli(Path.GetFileName(this.StandaloneCliPath));
        }
        catch (Exception ex)
        {
            var targetsFile = Path.Combine(Path.GetDirectoryName(this.BuildEngine.ProjectFileOfTaskNode), "build", "Tailwind.MSBuild.targets");
            this.Log.LogErrorFromException(ex, true, true, targetsFile);
        }

        return !this.Log.HasLoggedErrors;
    }

    public string GetFilePath()
    {
        // The file name is specific to the current platform and architecture
        var platform = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "macos"
                     : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "linux"
                     : RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows"
                     : throw new PlatformNotSupportedException("The TailwindCSS Standalone CLI does not support the current OS");

        var arch = ProcessorArchitecture.CurrentProcessArchitecture;

        if (arch == ProcessorArchitecture.AMD64)
            arch = "x64";
        else
            arch = arch.ToLower(CultureInfo.CurrentCulture);

        var fileName = $"tailwindcss-{platform}-{arch}";

        if (platform == "windows")
            fileName = $"{fileName}.exe";

        return Path.GetFullPath(Path.Combine(this.RootInstallPath, this.Version, fileName));
    }

    private void DownloadCli(string fileName)
    {
        using var client = new TailwindDownloader();

        var release = client.GetReleaseAsync(this.Version).GetAwaiter().GetResult();

        var asset = (release?.Assets.FirstOrDefault(a => a.Name.Equals(fileName, StringComparison.OrdinalIgnoreCase)))
                  ?? throw new Exception($"Unable to find a download link for '{fileName}' with Tailwind version '{this.Version}'");

        this.Log.LogMessage(MessageImportance.Low, "Downloading '{0}'", asset.DownloadUrl);

        var response = client.GetAssetAsync(asset).GetAwaiter().GetResult();

        this.Log.LogMessage(MessageImportance.Low, "Writing file to '{0}'", this.StandaloneCliPath);

        Directory.CreateDirectory(Path.GetDirectoryName(this.StandaloneCliPath));

        using (var file = File.OpenWrite(this.StandaloneCliPath))
        {
            file.Write(response, 0, response.Length);
            file.Close();
        }

        if (!File.Exists(this.StandaloneCliPath))
            throw new Exception($"Unable to download '{asset.Name}' to '{this.StandaloneCliPath}'");

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            SetPosixFilePermissions();
    }

    private void SetPosixFilePermissions()
    {
        // Grant execute permissions to current user
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "chmod",
                Arguments = $"+x {this.StandaloneCliPath}",
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
    }
}
