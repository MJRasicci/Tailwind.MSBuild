using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
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
			StandaloneCliPath = GetPathToExecutable();

			if (Log.HasLoggedErrors)
				return false;
			else if (File.Exists(StandaloneCliPath))
				return true;

			Directory.CreateDirectory(Path.Combine(RootInstallPath, Version));

			try
			{
				using (var client = new GitHubClient())
				{
					var release = (Version.Equals("latest") ? client.GetLatestReleaseAsync() : client.GetReleaseAsync(Version)).GetAwaiter().GetResult();
					var asset = release.Assets.FirstOrDefault(a => a.Name.Equals(Path.GetFileName(StandaloneCliPath), StringComparison.OrdinalIgnoreCase));

					if (asset != null)
					{
						client.GetAssetAsync(asset, StandaloneCliPath).GetAwaiter().GetResult();

						if (!File.Exists(StandaloneCliPath))
							Log.LogError($"Unable to download '{asset.Name}' to '{StandaloneCliPath}'");
					}
				}
			}
			catch (Exception ex)
			{
				Log.LogErrorFromException(ex);
			}

            return !Log.HasLoggedErrors;
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

			return Path.GetFullPath(Path.Combine(RootInstallPath, Version, fileName.ToLower()));
		}
	}
}
