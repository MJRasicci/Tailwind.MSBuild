namespace Tailwind.MSBuild.Tests;

using Xunit;
using Tailwind.MSBuild.Tests.Common;
using Tailwind.MSBuild.Tasks;

public class GetTailwindCLITests : IClassFixture<TaskFixture<GetTailwindCLI>>
{
    private readonly TaskFixture<GetTailwindCLI> fixture;

    public GetTailwindCLITests(TaskFixture<GetTailwindCLI> fixture)
    {
        this.fixture = fixture;
    }
}
