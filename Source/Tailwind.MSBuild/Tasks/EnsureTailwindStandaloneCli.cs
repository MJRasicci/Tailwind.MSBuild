using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Build.Framework;
using Tailwind.MSBuild.GitHub;

namespace Tailwind.MSBuild.Tasks
{
	/// <summary>
	///		An MSBuild task to verify the tailwindcss standalone-cli tool is available on the current machine.
	/// </summary>
	public class EnsureTailwindStandaloneCli : Microsoft.Build.Utilities.Task
	{
		/// <summary>
		///		The version tag of the tailwind release to use, defaults to "latest" for the most current release.
		/// </summary>
		[Required]
        public string Version { get; set; }

		/// <summary>
		///		The directory where the executable should be located. Defaults to the intermediate output directory.
		/// </summary>
		[Required]
        public string InstallPath { get; set; }

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
			var product = Product.Details;
			var platformFileName = PlatformCommandMap[(OSPlatform.Create(RuntimeInformation.OSDescription), RuntimeInformation.OSArchitecture)];
			StandaloneCliPath = Path.Combine(InstallPath, platformFileName);

			if (File.Exists(StandaloneCliPath))
				return true;

			try
			{
				using (var client = new HttpClient())
				{
					// The GitHub REST API requires a user-agent header for unauthenticated requests.
					client.DefaultRequestHeaders.Add("User-Agent", $"{product.Name}/{product.Version} {product.Company}");

					var endpoint = Version.StartsWith("v")
									? $"https://api.github.com/repos/MJRasicci/tailwindcss/releases/tags/{Version}"
									: "https://api.github.com/repos/MJRasicci/tailwindcss/releases/latest";

					var response = client.GetStringAsync(endpoint).GetAwaiter().GetResult();
					var release = JsonSerializer.Deserialize<TailwindRelease>(response);
					var asset = release.Assets.FirstOrDefault(a => a.Name.Equals(platformFileName, StringComparison.OrdinalIgnoreCase));

					if (asset != null)
					{
						var data = client.GetByteArrayAsync(asset.DownloadUrl).GetAwaiter().GetResult();

						using (var file = File.OpenWrite(StandaloneCliPath))
						{
							file.Write(data, 0, data.Length);
							file.Close();
						}
					}
				}
			}
			catch (Exception ex)
			{
				Log.LogErrorFromException(ex);
			}

            return !Log.HasLoggedErrors;
        }

		private static readonly Dictionary<(OSPlatform OS, Architecture Arch), string> PlatformCommandMap = new Dictionary<(OSPlatform OS, Architecture Arch), string>
		{
			{ (OSPlatform.Windows, Architecture.X64), "tailwindcss-windows-x64.exe" },
			{ (OSPlatform.Windows, Architecture.Arm64), "tailwindcss-windows-arm64.exe" },
			{ (OSPlatform.OSX, Architecture.X64), "tailwindcss-osx-x64" },
			{ (OSPlatform.OSX, Architecture.Arm64), "tailwindcss-osx-arm64" },
			{ (OSPlatform.Linux, Architecture.X64), "tailwindcss-linux-x64" },
			{ (OSPlatform.Linux, Architecture.Arm64), "tailwindcss-linux-arm64" },
			{ (OSPlatform.Linux, Architecture.Arm), "tailwindcss-linux-armv7" },
		};
	}
}
