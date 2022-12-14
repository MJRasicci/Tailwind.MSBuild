namespace Tailwind.MSBuild.Tasks;

using Tailwind.MSBuild.GitHub;

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
            // The file name is specific to the current platform and architecture
            var fileName = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? $"tailwindcss-macos-{ProcessorArchitecture.CurrentProcessArchitecture}"
                         : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? $"tailwindcss-linux-{ProcessorArchitecture.CurrentProcessArchitecture}"
                         // TODO: Expression should use CurrentProcessArchitecture after standalone cli v3.2.5 is released to include arm64 support
                         // For now just download the x64 binary and let windows emulation layer handle it on arm64 machines
                         : RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? $"tailwindcss-windows-x64.exe"
                         : throw new Exception("Unable to detect the proper platform and runtime for TailwindCSS");

            fileName = fileName.ToLower();

            this.StandaloneCliPath = Path.GetFullPath(Path.Combine(this.RootInstallPath, this.Version, fileName));

            Directory.CreateDirectory(Path.GetDirectoryName(this.StandaloneCliPath));

            using var client = new GitHubClient();

            var release = (this.Version.Equals("latest") ? client.GetLatestReleaseAsync() : client.GetReleaseAsync(this.Version)).GetAwaiter().GetResult();

            var asset = (release?.Assets.FirstOrDefault(a => a.Name.Equals(fileName, StringComparison.OrdinalIgnoreCase)))
                      ?? throw new Exception($"Unable to find a download link for '{fileName}' with Tailwind version '{this.Version}'");

            var response = client.GetAssetAsync(asset).GetAwaiter().GetResult();

            if (File.Exists(this.StandaloneCliPath))
                File.Delete(this.StandaloneCliPath);

            using (var file = File.OpenWrite(this.StandaloneCliPath))
            {
                file.Write(response, 0, response.Length);
                file.Close();
            }

            if (!File.Exists(this.StandaloneCliPath))
                throw new Exception($"Unable to download '{asset.Name}' to '{this.StandaloneCliPath}'");
        }
        catch (Exception ex)
        {
            this.Log.LogErrorFromException(ex, true, true, null);
        }

        return !this.Log.HasLoggedErrors;
    }
}
