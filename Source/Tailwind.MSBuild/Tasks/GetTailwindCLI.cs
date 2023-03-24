namespace Tailwind.MSBuild.Tasks;

using System.Globalization;
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
            var arch = ProcessorArchitecture.CurrentProcessArchitecture;

            if (arch == ProcessorArchitecture.AMD64)
                arch = "x64";
            else
                arch = arch.ToLower();

            // The file name is specific to the current platform and architecture
            var fileName = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? $"tailwindcss-macos-{arch}"
                         : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? $"tailwindcss-linux-{arch}"
                         : RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? $"tailwindcss-windows-{arch}.exe"
                         : throw new Exception("Unable to detect the proper platform and runtime for TailwindCSS");

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
            var targetsFile = Path.Combine(Path.GetDirectoryName(this.BuildEngine.ProjectFileOfTaskNode), "build", "Tailwind.MSBuild.targets");
            this.Log.LogErrorFromException(ex, true, true, targetsFile);
        }

        return !this.Log.HasLoggedErrors;
    }
}
