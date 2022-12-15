namespace Tailwind.MSBuild.Tests.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Moq;
using Tailwind.MSBuild.GitHub;

public class TaskFixture<T> where T : Microsoft.Build.Utilities.Task, new()
{
    public Mock<IBuildEngine> BuildEngine { get; }

    public List<BuildErrorEventArgs> Errors { get; }

    public TaskFixture()
    {
        this.BuildEngine = new Mock<IBuildEngine>();
        this.Errors = new List<BuildErrorEventArgs>();
        this.BuildEngine
                .Setup(_ => _.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(e => this.Errors.Add(e));
    }

    public T Prepare(Action<T> configure)
    {
        var task = new T
        {
            BuildEngine = this.BuildEngine.Object
        };

        configure(task);

        return task;
    }
}
