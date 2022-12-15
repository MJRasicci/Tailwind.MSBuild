namespace Tailwind.MSBuild.UnitTests;

using Moq;
using Tailwind.MSBuild.Tasks;
using Tailwind.MSBuild.UnitTests.Common;
using Xunit;

public class BuildTailwindCssTests : IClassFixture<TaskFixture<BuildTailwindCss>>
{
    private readonly TaskFixture<BuildTailwindCss> fixture;

    public BuildTailwindCssTests(TaskFixture<BuildTailwindCss> fixture)
    {
        this.fixture = fixture;
    }
}
