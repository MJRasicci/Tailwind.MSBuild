namespace Tailwind.MSBuild.Tests.Common;

using Moq;

public class TaskFixture<T> where T : Microsoft.Build.Utilities.Task, new()
{
    public Mock<IBuildEngine> BuildEngine { get; }

    public IList<BuildErrorEventArgs> Errors { get; }

    public TaskFixture()
    {
        this.BuildEngine = new Mock<IBuildEngine>();
        this.Errors = new List<BuildErrorEventArgs>();
        this.BuildEngine
                .Setup(_ => _.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(e => this.Errors.Add(e));
    }

    public T Prepare(Action<T>? configure = null)
    {
        var task = new T
        {
            BuildEngine = this.BuildEngine.Object
        };

        configure?.Invoke(task);

        return task;
    }
}
