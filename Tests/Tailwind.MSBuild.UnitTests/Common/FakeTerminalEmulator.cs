namespace Tailwind.MSBuild.Tests.Common;

internal static class FakeTerminalEmulator
{
    private const string UnixScriptTemplate = """
#!/usr/bin/env sh
printf '%s\n' "$@" > '__ARGS_FILE__'
printf '%s\n' "$$" > '__ARGS_FILE__.pid'

case "$1" in
  -e)
    shift
    __LAUNCH_COMMAND__
    ;;
  --)
    shift
    exec "$@"
    ;;
  --command)
    shift
    exec /bin/sh -lc "$1"
    ;;
  start)
    shift
    if [ "$1" = "--" ]; then
      shift
    fi
    exec "$@"
    ;;
  *)
    exec "$@"
    ;;
esac
""";

    public static string Create(string directory, string executableName, string argumentsFilePath, bool brokered = false)
    {
        if (OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Fake terminal emulators are only used in Linux tests.");

        Directory.CreateDirectory(directory);

        var scriptPath = Path.Combine(directory, executableName);
        var scriptContents = UnixScriptTemplate
            .Replace("__ARGS_FILE__", EscapeForSingleQuotedShell(argumentsFilePath), StringComparison.Ordinal)
            .Replace("__LAUNCH_COMMAND__", brokered ? "\"$@\" </dev/null >/dev/null 2>&1 &" : "exec \"$@\"", StringComparison.Ordinal);

        File.WriteAllText(scriptPath, scriptContents, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.SetUnixFileMode(scriptPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return scriptPath;
    }

    private static string EscapeForSingleQuotedShell(string value)
    {
        return value.Replace("'", "'\"'\"'", StringComparison.Ordinal);
    }
}
