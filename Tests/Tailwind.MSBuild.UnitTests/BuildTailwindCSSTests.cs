namespace Tailwind.MSBuild.Tests;

using Xunit;
using Tailwind.MSBuild.Tests.Common;
using Tailwind.MSBuild.Tasks;

public class BuildTailwindCSSTests : IClassFixture<TaskFixture<BuildTailwindCSS>>
{
    private readonly TaskFixture<BuildTailwindCSS> fixture;

    public BuildTailwindCSSTests(TaskFixture<BuildTailwindCSS> fixture)
    {
        this.fixture = fixture;
    }
}
