namespace Tailwind.MSBuild.Utilities;

using System.Text;

internal sealed class TailwindTerminalResolver
{
    private const int ExecuteAccess = 1;

    private static readonly TailwindTerminalDefinition[] TerminalDefinitions =
    [
        new("xdg-terminal-exec", BuildXdgTerminalExecArguments),
        new("x-terminal-emulator", BuildExecArguments),
        new("kgx", BuildDashDashArguments),
        new("gnome-terminal", BuildDashDashArguments),
        new("ptyxis", BuildDashDashArguments),
        new("konsole", BuildExecArguments),
        new("xfce4-terminal", BuildExecuteArguments),
        new("mate-terminal", BuildDashDashArguments),
        new("tilix", BuildTilixArguments),
        new("alacritty", BuildExecArguments),
        new("kitty", BuildDirectArguments),
        new("wezterm", BuildWeztermArguments),
        new("ghostty", BuildExecArguments),
        new("foot", BuildDirectArguments),
        new("xterm", BuildExecArguments)
    ];

    private readonly Func<string, bool> isExecutable;
    private readonly string[] pathDirectories;

    internal TailwindTerminalResolver(string? pathEnvironment = null, Func<string, bool>? isExecutable = null)
    {
        this.isExecutable = isExecutable ?? CanExecute;
        this.pathDirectories = (pathEnvironment ?? Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                               .Split([ Path.PathSeparator ], StringSplitOptions.RemoveEmptyEntries);
    }

    public IReadOnlyList<TailwindTerminalCommand> ResolveCandidates(string? preferredExecutable, out string? message)
    {
        message = null;

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return [];

        var candidates = new List<TailwindTerminalCommand>();
        TailwindTerminalCommand? preferred = null;

        if (!string.IsNullOrWhiteSpace(preferredExecutable))
        {
            preferred = TryResolvePreferredTerminal(preferredExecutable!);

            if (preferred != null)
                candidates.Add(preferred);
            else
                message = $"Configured Tailwind terminal '{preferredExecutable}' is unavailable or unsupported. Falling back to auto-detection.";
        }

        foreach (var definition in TerminalDefinitions)
        {
            var executablePath = ResolveExecutablePath(definition.Name);

            if (executablePath != null && !string.Equals(preferred?.ExecutablePath, executablePath, StringComparison.Ordinal))
                candidates.Add(new TailwindTerminalCommand(executablePath, definition.BuildArguments));
        }

        return candidates;
    }

    private TailwindTerminalCommand? TryResolvePreferredTerminal(string preferredExecutable)
    {
        var fileName = Path.GetFileName(preferredExecutable);
        var definition = TerminalDefinitions.FirstOrDefault(candidate => string.Equals(candidate.Name, fileName, StringComparison.OrdinalIgnoreCase));

        if (definition == null)
            return null;

        var executablePath = ResolveExecutablePath(preferredExecutable);
        return executablePath == null ? null : new TailwindTerminalCommand(executablePath, definition.BuildArguments);
    }

    private string? ResolveExecutablePath(string executable)
    {
        if (string.IsNullOrWhiteSpace(executable))
            return null;

        if (executable.Contains('/'))
            return this.isExecutable(executable) ? Path.GetFullPath(executable) : null;

        foreach (var directory in this.pathDirectories)
        {
            var fullPath = Path.Combine(directory, executable);

            if (this.isExecutable(fullPath))
                return Path.GetFullPath(fullPath);
        }

        return null;
    }

    private static bool CanExecute(string path)
    {
        return File.Exists(path) && Access(Encoding.UTF8.GetBytes(path + '\0'), ExecuteAccess) == 0;
    }

    [DllImport("libc", EntryPoint = "access")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    private static extern int Access(byte[] path, int mode);

    private static IReadOnlyList<string> BuildXdgTerminalExecArguments(string scriptPath, string? workingDirectory)
    {
        return string.IsNullOrWhiteSpace(workingDirectory)
            ? BuildDashDashArguments(scriptPath, workingDirectory)
            : [ $"--dir={workingDirectory}", .. BuildDashDashArguments(scriptPath, workingDirectory) ];
    }

    private static IReadOnlyList<string> BuildExecArguments(string scriptPath, string? _)
    {
        return [ "-e", .. BuildDirectArguments(scriptPath, null) ];
    }

    private static IReadOnlyList<string> BuildDashDashArguments(string scriptPath, string? _)
    {
        return [ "--", .. BuildDirectArguments(scriptPath, null) ];
    }

    private static IReadOnlyList<string> BuildDirectArguments(string scriptPath, string? _)
    {
        return [ "/bin/sh", scriptPath ];
    }

    private static IReadOnlyList<string> BuildWeztermArguments(string scriptPath, string? _)
    {
        return [ "start", .. BuildDashDashArguments(scriptPath, null) ];
    }

    private static IReadOnlyList<string> BuildExecuteArguments(string scriptPath, string? _)
    {
        return [ "--execute", .. BuildDirectArguments(scriptPath, null) ];
    }

    private static IReadOnlyList<string> BuildTilixArguments(string scriptPath, string? _)
    {
        // Tilix passes its --command value through GLib's shell argument parser.
        var quotedScriptPath = "'" + scriptPath.Replace("'", "'\"'\"'") + "'";
        return [ "--command", $"/bin/sh {quotedScriptPath}" ];
    }

    private sealed class TailwindTerminalDefinition
    {
        public TailwindTerminalDefinition(string name, Func<string, string?, IReadOnlyList<string>> buildArguments)
        {
            this.Name = name;
            this.BuildArguments = buildArguments;
        }

        public string Name { get; }

        public Func<string, string?, IReadOnlyList<string>> BuildArguments { get; }
    }
}

internal sealed class TailwindTerminalCommand
{
    private readonly Func<string, string?, IReadOnlyList<string>> buildArguments;

    public TailwindTerminalCommand(string executablePath, Func<string, string?, IReadOnlyList<string>> buildArguments)
    {
        this.ExecutablePath = executablePath;
        this.buildArguments = buildArguments;
    }

    public string ExecutablePath { get; }

    public IReadOnlyList<string> BuildArguments(string scriptPath, string? workingDirectory)
    {
        return this.buildArguments(scriptPath, workingDirectory);
    }
}
