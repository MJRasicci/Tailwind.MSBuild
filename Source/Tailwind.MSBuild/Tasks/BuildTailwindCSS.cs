namespace Tailwind.MSBuild.Tasks;

/// <summary>
///     An MSBuild task to build TailwindCSS within a project.
/// </summary>
public class BuildTailwindCSS : Microsoft.Build.Utilities.Task
{
    /// <summary>
    ///		The full path to the standalone-cli executable.
    /// </summary>
    [Required]
	public string StandaloneCliPath { get; set; } = string.Empty;

    /// <summary>
    ///		The directory containing the tailwind configuration files.
    /// </summary>
    [Required]
    public string ConfigDir { get; set; } = string.Empty;

    /// <summary>
    ///		The name of the tailwind configuration file.
    /// </summary>
    [Required]
	public string ConfigFile { get; set; } = string.Empty;

    /// <summary>
    ///     The name of the input css file.
    /// </summary>
    [Required]
    public string InputFile { get; set; } = string.Empty;

    /// <summary>
    ///     The path where the output css file will be located.
    /// </summary>
    [Required]
    public string OutputFile { get; set; } = string.Empty;

    /// <summary>
    ///     Whether the generated css should be minified or not.
    /// </summary>
    [Required]
    public bool Minify { get; set; }

    /// <summary>
    ///     Whether to run TailwindCSS in watch mode.
    /// </summary>
    [Required]
    public bool Watch { get; set; }

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
        // Init
        if (!File.Exists(Path.Combine(this.ConfigDir, this.ConfigFile)))
        {
            Directory.CreateDirectory(this.ConfigDir);
            RunCli($"init {this.ConfigFile} --postcss");
        }

        this.InputFile = Path.Combine(this.ConfigDir, this.InputFile);

        if (!File.Exists(this.InputFile))
        {
            using var file = File.CreateText(this.InputFile);
            file.WriteLine("@tailwind base;");
            file.WriteLine("@tailwind components;");
            file.WriteLine("@tailwind utilities;");
            file.Close();
        }

        // Build
        RunCli($"-c \"{this.ConfigFile}\" -i \"{this.InputFile}\" -o \"{this.OutputFile}\"{(this.Minify ? " -m" : string.Empty)}");

        this.GeneratedCssFile = this.OutputFile;

        return !this.Log.HasLoggedErrors;
    }

	private void RunCli(string args)
	{
        // The CLI process runs entirely in the background within the directory containing the configuration file
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                WorkingDirectory = this.ConfigDir,
                FileName = this.StandaloneCliPath,
                Arguments = args,
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

        if (!process.Start())
            this.Log.LogError($"Unable to start process from executable {process.StartInfo.FileName}");

        process.BeginErrorReadLine();
        process.BeginOutputReadLine();
        process.WaitForExit();
    }
}
