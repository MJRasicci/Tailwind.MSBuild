namespace Tailwind.MSBuild.Tests.Common;

using Tailwind.MSBuild.Utilities;

public class TailwindDownloaderFixture : IDisposable
{
    private bool disposed;

    internal TailwindDownloader Client { get; private set; }

    public TailwindDownloaderFixture()
    {
        this.Client = new TailwindDownloader();
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (this.disposed)
            return;

        if (disposing)
            this.Client.Dispose();

        this.disposed = true;
    }
}
