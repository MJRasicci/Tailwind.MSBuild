namespace Tailwind.MSBuild.Tasks;

using Tailwind.MSBuild.Utilities;

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
    ///     The path to the lock file used to prevent duplicate Tailwind CLI instances.
    /// </summary>
    [Required]
    public string WatchLockFile { get; set; } = string.Empty;

    /// <summary>
    ///     An optional terminal emulator executable or absolute path used for Linux watch mode.
    /// </summary>
    public string TerminalExecutable { get; set; } = string.Empty;

    /// <summary>
    ///     The absolute path to the file where the css was generated.
    /// </summary>
    [Output]
    public string GeneratedCssFile { get; set; } = string.Empty;

    internal TailwindCliRunner CliRunner { get; set; } = new();

    /// <summary>
    ///     Builds tailwindcss for the current project.
    /// </summary>
    public override bool Execute()
	{
        this.ProjectDirectory = Path.GetFullPath(this.ProjectDirectory);
        this.StandaloneCliPath = Path.GetFullPath(this.StandaloneCliPath);
        this.ConfigDir = Path.GetFullPath(this.ConfigDir);
        this.InputFile = ResolvePath(this.InputFile, this.ConfigDir);
        this.OutputFile = ResolvePath(this.OutputFile, this.ConfigDir);

        Directory.CreateDirectory(this.ConfigDir);

        if (!File.Exists(this.InputFile))
        {
            var sourcePath = GetPathToSourceDirectory();

            Directory.CreateDirectory(Path.GetDirectoryName(this.InputFile)!);

            using var file = File.CreateText(this.InputFile);
            file.WriteLine("@import \"tailwindcss\";");

            if (!string.IsNullOrWhiteSpace(sourcePath))
                file.WriteLine($"@source \"{sourcePath}\";");
        }

        this.GeneratedCssFile = string.Empty;
        var succeeded = this.CliRunner.Run(
            new TailwindCliInvocation
            {
                ExecutablePath = this.StandaloneCliPath,
                WorkingDirectory = this.ConfigDir,
                Arguments = BuildCliArguments(),
                OutputFile = this.OutputFile,
                Watch = this.Watch,
                ProjectDirectory = this.ProjectDirectory,
                WatchLockFile = this.WatchLockFile,
                WatchTerminalExecutable = this.TerminalExecutable
            },
            this.Log);

        if (succeeded)
            this.GeneratedCssFile = this.OutputFile;

        return succeeded && !this.Log.HasLoggedErrors;
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
        if (!path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) &&
            !path.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal))
        {
            path += Path.DirectorySeparatorChar;
        }

        return path;
    }

    private static string NormalizeSlashes(string path)
    {
        return path.Replace('\\', '/');
    }

    private static string ResolvePath(string path, string relativeToDirectory)
    {
        if (Path.IsPathRooted(path))
            return Path.GetFullPath(path);

        return Path.GetFullPath(Path.Combine(relativeToDirectory, path));
    }

    private List<string> BuildCliArguments()
    {
        var arguments = new List<string>
        {
            "-i",
            this.InputFile,
            "-o",
            this.OutputFile
        };

        if (this.Minify)
            arguments.Add("-m");

        return arguments;
    }
}
