namespace Tailwind.MSBuild.Tests;

using FluentAssertions;
using Tailwind.MSBuild.Utilities;
using Xunit;

public sealed class TailwindLockFileTests
{
    [Fact]
    public void Read_WhenStreamIsEmpty_ReturnsEmptyLockFile()
    {
        using var stream = new MemoryStream();

        var lockFile = TailwindLockFile.Read(stream, out var isCorrupt);

        isCorrupt.Should().BeFalse();
        lockFile.Entries.Should().BeEmpty();
    }

    [Fact]
    public void Read_WhenJsonIsInvalid_SetsCorruptFlag()
    {
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes("{invalid-json"));

        var lockFile = TailwindLockFile.Read(stream, out var isCorrupt);

        isCorrupt.Should().BeTrue();
        lockFile.Entries.Should().BeEmpty();
    }

    [Fact]
    public void Write_ThenRead_RoundTripsEntries()
    {
        using var stream = new MemoryStream();
        var now = DateTimeOffset.UtcNow;
        var expectedEntries = new[]
        {
            new TailwindLockFileEntry
            {
                ProcessId = 123,
                ProjectDirectory = "/repo/project-a",
                StartedAtUtc = now
            },
            new TailwindLockFileEntry
            {
                ProcessId = 456,
                ProjectDirectory = "/repo/project-b",
                StartedAtUtc = now.AddMinutes(1)
            }
        };

        new TailwindLockFile(expectedEntries).Write(stream);
        var actual = TailwindLockFile.Read(stream, out var isCorrupt);

        isCorrupt.Should().BeFalse();
        actual.Entries.Should().HaveCount(2);
        actual.Entries[0].ProcessId.Should().Be(123);
        actual.Entries[0].ProjectDirectory.Should().Be("/repo/project-a");
        actual.Entries[1].ProcessId.Should().Be(456);
        actual.Entries[1].ProjectDirectory.Should().Be("/repo/project-b");
    }
}
