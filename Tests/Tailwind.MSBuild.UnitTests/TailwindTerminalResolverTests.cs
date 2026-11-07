namespace Tailwind.MSBuild.Tests;

using FluentAssertions;
using Tailwind.MSBuild.Utilities;
using Xunit;

public sealed class TailwindTerminalResolverTests
{
    [Fact]
    public void Resolve_OnLinux_PrefersXdgTerminalExec()
    {
        if (!OperatingSystem.IsLinux())
            return;

        var executablePaths = new HashSet<string>(StringComparer.Ordinal)
        {
            "/tools/xdg-terminal-exec",
            "/tools/x-terminal-emulator"
        };

        var resolver = new TailwindTerminalResolver(
            pathEnvironment: "/tools",
            isExecutable: executablePaths.Contains);

        var candidates = resolver.ResolveCandidates(preferredExecutable: null, out var message);

        message.Should().BeNull();
        candidates.Select(candidate => candidate.ExecutablePath).Should().Equal("/tools/xdg-terminal-exec", "/tools/x-terminal-emulator");
        candidates[0].BuildArguments("/tmp/tailwind-watch/run.sh", "/repo/project space").Should().Equal("--dir=/repo/project space", "--", "/bin/sh", "/tmp/tailwind-watch/run.sh");
    }

    [Fact]
    public void Resolve_OnLinux_UsesOverrideBeforeAutoDetection()
    {
        if (!OperatingSystem.IsLinux())
            return;

        var executablePaths = new HashSet<string>(StringComparer.Ordinal)
        {
            "/usr/bin/xdg-terminal-exec",
            "/custom/gnome-terminal"
        };

        var resolver = new TailwindTerminalResolver(
            pathEnvironment: string.Join(Path.PathSeparator, "/usr/bin", "/custom"),
            isExecutable: executablePaths.Contains);

        var candidates = resolver.ResolveCandidates("/custom/gnome-terminal", out var message);

        message.Should().BeNull();
        candidates.Select(candidate => candidate.ExecutablePath).Should().Equal("/custom/gnome-terminal", "/usr/bin/xdg-terminal-exec");
        candidates[0].BuildArguments("/tmp/tailwind-watch/run.sh", null).Should().Equal("--", "/bin/sh", "/tmp/tailwind-watch/run.sh");
    }

    [Fact]
    public void Resolve_OnLinux_FallsBackWhenOverrideIsUnsupported()
    {
        if (!OperatingSystem.IsLinux())
            return;

        var resolver = new TailwindTerminalResolver(
            pathEnvironment: "/usr/bin",
            isExecutable: path => path == "/usr/bin/x-terminal-emulator");

        var candidates = resolver.ResolveCandidates("/custom/not-a-supported-terminal", out var message);

        message.Should().NotBeNull();
        candidates.Should().ContainSingle().Which.ExecutablePath.Should().Be("/usr/bin/x-terminal-emulator");
    }

    [Fact]
    public void Resolve_OnLinux_SkipsNonExecutableFilesAndDirectories()
    {
        if (!OperatingSystem.IsLinux())
            return;

        var directory = Directory.CreateTempSubdirectory("tailwind-términals-");

        try
        {
            var unavailable = Path.Combine(directory.FullName, "xdg-terminal-exec");
            var executable = Path.Combine(directory.FullName, "ptyxis");
            Directory.CreateDirectory(Path.Combine(directory.FullName, "x-terminal-emulator"));
            File.WriteAllText(unavailable, "#!/bin/sh\n");
            File.SetUnixFileMode(unavailable, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            File.WriteAllText(executable, "#!/bin/sh\n");
            File.SetUnixFileMode(executable, UnixFileMode.UserRead | UnixFileMode.UserExecute);

            var resolver = new TailwindTerminalResolver(pathEnvironment: directory.FullName);
            var candidates = resolver.ResolveCandidates(unavailable, out var message);

            message.Should().Contain("unavailable");
            candidates.Should().ContainSingle().Which.ExecutablePath.Should().Be(executable);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void Resolve_OnLinux_UsesXfceExecuteWithoutFlatteningArguments()
    {
        if (!OperatingSystem.IsLinux())
            return;

        var resolver = new TailwindTerminalResolver(
            pathEnvironment: "/tools",
            isExecutable: path => path == "/tools/xfce4-terminal");

        var terminal = resolver.ResolveCandidates("xfce4-terminal", out _).Should().ContainSingle().Which;

        terminal.BuildArguments("/tmp/tailwind watch/run.sh", null).Should().Equal("--execute", "/bin/sh", "/tmp/tailwind watch/run.sh");
    }

    [Fact]
    public void Resolve_OnLinux_UsesTilixCommandWithShellArgumentQuoting()
    {
        if (!OperatingSystem.IsLinux())
            return;

        var resolver = new TailwindTerminalResolver(
            pathEnvironment: "/tools",
            isExecutable: path => path == "/tools/tilix");

        var terminal = resolver.ResolveCandidates("tilix", out _).Should().ContainSingle().Which;

        terminal.BuildArguments("/tmp/Tailwind's watch/run.sh", null).Should().Equal("--command", "/bin/sh '/tmp/Tailwind'\"'\"'s watch/run.sh'");
    }
}
