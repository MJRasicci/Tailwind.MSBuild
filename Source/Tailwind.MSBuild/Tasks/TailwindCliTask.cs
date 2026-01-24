namespace Tailwind.MSBuild.Tasks;

// Prevent conflicts with System.Reflection.ProcessorArchitecture
using ProcessorArchitecture = Microsoft.Build.Utilities.ProcessorArchitecture;

public abstract class TailwindCliTask : ToolTask
{
    /// <summary>
    ///		The path to the TailwindCSS standalone CLI executable.
    /// </summary>
    [Required]
    public string TailwindInstallPath { get; set; } = string.Empty;

    public override bool Execute()
    {
        var result = base.Execute();

        if (!result)
            this.Log.LogError("TailwindCSS build failed.");

        return result;
    }

    protected override string ToolName => "TailwindCSS";

    protected override string GenerateFullPathToTool() => Path.Combine(this.TailwindInstallPath, GetPlatformSpecificExecutable());

    private static string GetPlatformSpecificExecutable()
    {
        // The file name is specific to the current platform and architecture
        var platform = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "macos"
                        : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "linux"
                        : RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows"
                        : throw new PlatformNotSupportedException();

        var arch = ProcessorArchitecture.CurrentProcessArchitecture switch
        {
            ProcessorArchitecture.AMD64 => "x64",
            ProcessorArchitecture.ARM64 => "arm64",
            ProcessorArchitecture.ARM => "armv7",
            _ => throw new PlatformNotSupportedException()
        };

        var fileName = $"tailwindcss-{platform}-{arch}";

        if (platform is "windows")
            fileName = $"{fileName}.exe";

        return fileName;
    }
}
