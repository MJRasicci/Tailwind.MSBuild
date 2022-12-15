namespace Tailwind.MSBuild.Tasks;

using Microsoft.Build.Framework;
using System.Diagnostics;
using System.IO;

/// <summary>
///     An MSBuild task to build TailwindCSS within a project.
/// </summary>
public class BuildTailwindCss : Microsoft.Build.Utilities.Task
{
    /// <summary>
    ///     The directory containing a tailwind configuration (tailwind.config.js) file.
    /// </summary>
    [Required]
    public string TailwindConfigDir { get; set; } = string.Empty;

    /// <summary>
    ///		The full path to the standalone-cli executable.
    /// </summary>
    [Required]
		public string StandaloneCliPath { get; set; } = string.Empty;

    /// <summary>
    ///		The name of the file containing the tailwind configuration.
    /// </summary>
    [Required]
		public string TailwindConfigFile { get; set; } = string.Empty;

    /// <summary>
    ///     The file name of the input css file.
    /// </summary>
    [Required]
    public string InputFileName { get; set; } = string.Empty;

    /// <summary>
    ///     The file path where the output css will be located.
    /// </summary>
    [Required]
    public string OutputFilePath { get; set; } = string.Empty;

    /// <summary>
    ///     Whether the generated css should be minified or not.
    /// </summary>
    [Required]
    public bool Minify { get; set; }

	/// <summary>
	///     The absolute path to the file where the css was generated.
	/// </summary>
	[Output]
    public string GeneratedCssFile { get; set; } = string.Empty;

    /// <summary>
    ///     Builds tailwindcss for the current project.
    /// </summary>
    public override bool Execute()
	{
        this.TailwindConfigDir = Path.GetFullPath(this.TailwindConfigDir);
			Directory.CreateDirectory(this.TailwindConfigDir);

		if (!File.Exists(Path.Combine(this.TailwindConfigDir, this.TailwindConfigFile)))
			RunCli("init --full");

        if (!File.Exists(Path.Combine(this.TailwindConfigDir, this.InputFileName)))
        {
            using var file = File.CreateText(Path.Combine(this.TailwindConfigDir, this.InputFileName));

            file.WriteLine("@tailwind base;");
            file.WriteLine("@tailwind components;");
            file.WriteLine("@tailwind utilities;");

            file.Close();
        }

        RunCli($"-i {this.InputFileName} -o {this.OutputFilePath}{(this.Minify ? " -m" : string.Empty)}");
					
        return !this.Log.HasLoggedErrors;
    }

	private void RunCli(string args)
	{
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                WorkingDirectory = this.TailwindConfigDir,
                FileName = this.StandaloneCliPath,
                Arguments = args,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            }
        };

        process.ErrorDataReceived += (sender, e) => this.Log.LogError(e.Data);
        process.OutputDataReceived += (sender, e) => this.Log.LogMessage(e.Data);

        process.Start();
        process.WaitForExit();
    }
}
