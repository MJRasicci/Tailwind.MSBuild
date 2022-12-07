namespace Tailwind.MSBuild.Tests;

using Tailwind.MSBuild.Tasks;

[TestClass]
public class BuildTailwindCssTests
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

    [TestMethod]
    public void BuildTailwindCss_Succeeds()
    {
		// Arrange
		var buildTailwindCss = new BuildTailwindCss
		{
			TailwindConfigDir = ".\\TestData\\Input\\",
			InputFileName = "input.css",
			OutputFile = "css/tailwind.css",
			Minify = true,
			BuildEngine = buildEngine.Object
		};

		// Act
		var success = buildTailwindCss.Execute();

        // Assert
        Assert.IsTrue(success);
        Assert.AreEqual(errors.Count, 0);
        Assert.IsTrue(File.Exists(buildTailwindCss.StandaloneCliPath));
        Assert.IsTrue(File.ReadLines(buildTailwindCss.GeneratedCssFile).SequenceEqual(File.ReadLines(".\\TestData\\Output\\tailwind.css")));

        // Cleanup
        File.Delete(buildTailwindCss.GeneratedCssFile);
    }
}
