namespace Tailwind.MSBuild.Tests;

using Moq;
using Tailwind.MSBuild.Tasks;
using Tailwind.MSBuild.Tests.Common;
using Xunit;

public class BuildTailwindCssTests : IClassFixture<TaskFixture<BuildTailwindCss>>
{
    private readonly TaskFixture<BuildTailwindCss> fixture;

    public BuildTailwindCssTests(TaskFixture<BuildTailwindCss> fixture)
    {
        this.fixture = fixture;
    }
}
