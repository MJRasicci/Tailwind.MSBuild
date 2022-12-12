using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Build.Framework;
using Tailwind.MSBuild.GitHub;

namespace Tailwind.MSBuild.Tasks
{
    /// <summary>
    ///		An MSBuild task to verify the tailwindcss standalone-cli tool is available on the current machine and a configuration exists.
    /// </summary>
    public class InitializeTailwind : Microsoft.Build.Utilities.Task
	{
		/// <summary>
		///		The version tag of the tailwind release to use, defaults to "latest" for the most current release.
		/// </summary>
		[Required]
        public string Version { get; set; }

		/// <summary>
		///		The directory where the executable should be located.
		/// </summary>
		[Required]
        public string RootInstallPath { get; set; }

		/// <summary>
		///		The full path to the executable.
		/// </summary>
		[Output]
        public string StandaloneCliPath { get; set; }

		/// <summary>
		///		Checks to see if the standalone-cli tool is available at the expected install path, otherwise downloads it from github.
		/// </summary>
		public override bool Execute()
		{
			this.StandaloneCliPath = GetPathToExecutable();

			if (this.Log.HasLoggedErrors)
				return false;
			else if (File.Exists(this.StandaloneCliPath))
				return true;

			Directory.CreateDirectory(Path.Combine(this.RootInstallPath, this.Version));

			try
			{
				using (var client = new GitHubClient())
				{
					var release = (this.Version.Equals("latest") ? client.GetLatestReleaseAsync() : client.GetReleaseAsync(this.Version)).GetAwaiter().GetResult();
					var asset = release.Assets.FirstOrDefault(a => a.Name.Equals(Path.GetFileName(this.StandaloneCliPath), StringComparison.OrdinalIgnoreCase));

					if (asset != null)
					{
						client.GetAssetAsync(asset, this.StandaloneCliPath).GetAwaiter().GetResult();

						if (!File.Exists(this.StandaloneCliPath))
                            this.Log.LogError($"Unable to download '{asset.Name}' to '{this.StandaloneCliPath}'");
					}
				}
			}
			catch (Exception ex)
			{
                this.Log.LogErrorFromException(ex);
			}

            return !this.Log.HasLoggedErrors;
        }

		private string GetPathToExecutable()
		{
			var fileName = string.Empty;

			if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
				fileName = $"tailwindcss-macos-{RuntimeInformation.ProcessArchitecture}";
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
				fileName = $"tailwindcss-linux-{RuntimeInformation.ProcessArchitecture}";
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				fileName = $"tailwindcss-windows-{RuntimeInformation.ProcessArchitecture}.exe";

			return Path.GetFullPath(Path.Combine(this.RootInstallPath, this.Version, fileName.ToLower()));
		}
	}
}
