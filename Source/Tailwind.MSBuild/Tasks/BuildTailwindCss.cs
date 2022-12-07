using Microsoft.Build.Framework;
using System;
using System.Diagnostics;
using System.IO;

namespace Tailwind.MSBuild.Tasks
{
    /// <summary>
    ///     An MSBuild task to build TailwindCSS within a project.
    /// </summary>
    public class BuildTailwindCss : Microsoft.Build.Utilities.Task
    {
        /// <summary>
        ///     The directory containing a tailwind configuration (tailwind.config.js) file.
        /// </summary>
        [Required]
        public string TailwindConfigDir { get; set; }

		/// <summary>
		///		The full path to the standalone-cli executable.
		/// </summary>
		[Required]
		public string StandaloneCliPath { get; set; }

		/// <summary>
		///		The name of the file containing the tailwind configuration.
		/// </summary>
		[Required]
		public string TailwindConfigFile { get; set; }

		/// <summary>
		///     The file name of the input css file.
		/// </summary>
		[Required]
        public string InputFileName { get; set; }

		/// <summary>
		///     The file path where the output css will be located.
		/// </summary>
		[Required]
        public string OutputFilePath { get; set; }

		/// <summary>
		///     Whether the generated css should be minified or not.
		/// </summary>
		[Required]
        public bool Minify { get; set; }

		/// <summary>
		///     The absolute path to the file where the css was generated.
		/// </summary>
		[Output]
        public string GeneratedCssFile { get; set; }

		/// <summary>
		///     Builds tailwindcss for the current project.
		/// </summary>
		public override bool Execute()
		{
			TailwindConfigDir = Path.GetFullPath(TailwindConfigDir);
			Directory.CreateDirectory(TailwindConfigDir);

			if (!File.Exists(Path.Combine(TailwindConfigDir, TailwindConfigFile)))
				RunCli("init --full");

			if (!File.Exists(Path.Combine(TailwindConfigDir, InputFileName)))
				using (var file = File.CreateText(Path.Combine(TailwindConfigDir, InputFileName)))
				{
					file.WriteLine("@tailwind base;");
					file.WriteLine("@tailwind components;");
					file.WriteLine("@tailwind utilities;");

					file.Close();
				}

			RunCli($"-i {InputFileName} -o {OutputFilePath}{(Minify ? " -m" : string.Empty)}");
					
            return !Log.HasLoggedErrors;
        }

		private void RunCli(string args)
		{
			using (var process = new Process
			{
				StartInfo = new ProcessStartInfo
				{
					WorkingDirectory = TailwindConfigDir,
					FileName = StandaloneCliPath,
					Arguments = args,
					CreateNoWindow = true,
					UseShellExecute = false,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
				}
			})
			{
				process.ErrorDataReceived += (sender, e) => { Log.LogError(e.Data); };
				process.OutputDataReceived += (sender, e) => { Log.LogMessage(e.Data); };

				process.Start();
				process.WaitForExit();
			}
		}
    }
}
