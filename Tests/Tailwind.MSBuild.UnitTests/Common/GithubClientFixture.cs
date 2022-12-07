namespace Tailwind.MSBuild.UnitTests.Common;

using Tailwind.MSBuild.GitHub;

public class GithubClientFixture : IDisposable
{
	private bool disposed;

	internal GitHubClient Client { get; private set; }

    public GithubClientFixture()
	{
		this.Client = new GitHubClient();
	}

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
	{
		if (!this.disposed)
		{
			if (disposing)
				this.Client.Dispose();

			this.disposed = true;
		}
	}
}
