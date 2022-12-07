namespace Tailwind.MSBuild.Tests;

[TestClass]
public class EnsureTailwindStandaloneCliTests
{
	private Mock<IBuildEngine> buildEngine = null!;
	private List<BuildErrorEventArgs> errors = null!;

	[TestInitialize()]
	public void Startup()
	{
		buildEngine = new Mock<IBuildEngine>();
		errors = new List<BuildErrorEventArgs>();
		buildEngine.Setup(x => x.LogErrorEvent(It.IsAny<BuildErrorEventArgs>())).Callback<BuildErrorEventArgs>(e => errors.Add(e));
	}

}
