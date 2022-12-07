namespace Tailwind.MSBuild.UnitTests;

using Moq;

public class BuildTailwindCssTests
{
	private Mock<IBuildEngine> BuildEngine { get; set; } = null!;

	private List<BuildErrorEventArgs> Errors { get; set; } = null!;
}
