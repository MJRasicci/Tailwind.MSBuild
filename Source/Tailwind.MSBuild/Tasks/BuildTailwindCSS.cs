namespace Tailwind.MSBuild.Tasks;

/// <summary>
///     An MSBuild task to build TailwindCSS within a project.
/// </summary>
public class BuildTailwindCSS : Microsoft.Build.Utilities.Task
{
    /// <summary>
    ///     The value of MSBuildProjectDirectory.
    /// </summary>
    [Required]
    public string ProjectDirectory { get; set; } = string.Empty;

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
        this.InputFile = Path.Combine(this.ConfigDir, this.InputFile);

        if (!File.Exists(this.InputFile))
        {
            var sourcePath = GetPathToSourceDirectory();

            using var file = File.CreateText(this.InputFile);
            file.WriteLine("@import \"tailwindcss\";");

            if (!string.IsNullOrWhiteSpace(sourcePath))
                file.WriteLine($"@source \"{sourcePath}\";");

            file.Close();
        }

        // Build
        RunCli($"-i \"{this.InputFile}\" -o \"{this.OutputFile}\"{(this.Minify ? " -m" : string.Empty)}{(this.Watch ? " --watch" : string.Empty)}");

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
                UseShellExecute = this.Watch,
                RedirectStandardOutput = !this.Watch,
                RedirectStandardError = !this.Watch,
                CreateNoWindow = !this.Watch
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

        if (!this.Watch)
        {
            process.BeginErrorReadLine();
            process.BeginOutputReadLine();
            process.WaitForExit();
        }
    }

    private string? GetPathToSourceDirectory()
    {
        // Normalize to full paths and ensure trailing separators for directory semantics
        var sourceDir = EnsureTrailingSeparator(Path.GetFullPath(this.ConfigDir));
        var destDir = EnsureTrailingSeparator(Path.GetFullPath(this.ProjectDirectory));

        var sourceUri = new Uri(sourceDir, UriKind.Absolute);
        var destUri = new Uri(destDir, UriKind.Absolute);

        // Different volumes / roots -> return absolute destination (normalized)
        if (!string.Equals(sourceUri.Scheme, destUri.Scheme, StringComparison.OrdinalIgnoreCase))
            return NormalizeSlashes(destDir);

        var relativeUri = sourceUri.MakeRelativeUri(destUri);

        var relativePath = Uri.UnescapeDataString(relativeUri.ToString());

        // Convert URI separators to POSIX path separators
        return NormalizeSlashes(relativePath);
    }

    private static string EnsureTrailingSeparator(string path)
    {
        if (!path.EndsWith(Path.DirectorySeparatorChar.ToString()) &&
            !path.EndsWith(Path.AltDirectorySeparatorChar.ToString()))
        {
            path += Path.DirectorySeparatorChar;
        }

        return path;
    }

    private static string NormalizeSlashes(string path)
    {
        return path.Replace('\\', '/');
    }
}
