namespace Tailwind.MSBuild.Tasks;

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Build.Framework;
using Tailwind.MSBuild.GitHub;

/// <summary>
///		An MSBuild task to verify the tailwindcss standalone-cli tool is available on the current machine and a configuration exists.
/// </summary>
public class InitializeTailwind : Microsoft.Build.Utilities.Task
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
            DownloadTailwind();
        }
        catch (Exception ex)
        {
            this.Log.LogErrorFromException(ex);
        }

        return !this.Log.HasLoggedErrors;
    }

    private void DownloadTailwind()
    {
        // The file name is specific to the current platform and architecture
        var fileName = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? $"tailwindcss-macos-{RuntimeInformation.ProcessArchitecture}"
                     : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? $"tailwindcss-linux-{RuntimeInformation.ProcessArchitecture}"
                     : RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? $"tailwindcss-windows-{RuntimeInformation.ProcessArchitecture}.exe"
                     : throw new Exception("Unable to detect the proper platform and runtime for TailwindCSS");

        this.StandaloneCliPath = Path.GetFullPath(Path.Combine(this.RootInstallPath, this.Version, fileName.ToLower()));

        Directory.CreateDirectory(this.StandaloneCliPath);

        using var client = new GitHubClient();

        var release = (this.Version.Equals("latest") ? client.GetLatestReleaseAsync() : client.GetReleaseAsync(this.Version)).GetAwaiter().GetResult();
        var asset = (release?.Assets.FirstOrDefault(a => a.Name.Equals(fileName)))
                  ?? throw new Exception("Unable to find a TailwindCSS CLI release for the current platform and runtime");

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
}
