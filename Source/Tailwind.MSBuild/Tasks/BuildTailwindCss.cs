using Microsoft.Build.Framework;
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
		///     The file name of the input css file.
		/// </summary>
		[Required]
        public string InputFile { get; set; }

		/// <summary>
		///     The file path where the output will be located.
		/// </summary>
		[Required]
        public string OutputFile { get; set; }

		/// <summary>
		///     Whether the generated css should be minified or not.
		/// </summary>
		[Required]
        public bool Minify { get; set; }

		/// <summary>
		///     The file name where the css was generated.
		/// </summary>
		[Output]
        public string GeneratedCssFile { get; set; }

		/// <summary>
		///     Builds tailwindcss for the current project.
		/// </summary>
		public override bool Execute()
        {
            if (!File.Exists(Path.Combine(TailwindConfigDir, "tailwind.config.js")))
			{
				var init = new ProcessStartInfo
				{
					WorkingDirectory = TailwindConfigDir,
					FileName = StandaloneCliPath,
					Arguments = "init",
					CreateNoWindow = true
				};

				var initProc = Process.Start(init);
				initProc.WaitForExit();

				if (initProc.ExitCode != 0)
				{
					Log.LogError($"Failed to initialize a new tailwindcss project. Process exited with status code {initProc.ExitCode}");
				}
			}

            var build = new ProcessStartInfo
            {
                WorkingDirectory = TailwindConfigDir,
                FileName = StandaloneCliPath,
                Arguments = $"-i {InputFile} -o {OutputFile}{(Minify ? " -m" : string.Empty )}",
                CreateNoWindow = true
            };

            var buildProc = Process.Start(build);
			buildProc.WaitForExit();

            if (buildProc.ExitCode != 0)
            {
                Log.LogError($"Failed to build tailwindcss. Process exited with status code {buildProc.ExitCode}");
            }

            return !Log.HasLoggedErrors;
        }
    }
}
