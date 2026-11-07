[assembly: FluentAssertions.Extensibility.AssertionEngineInitializer(
    typeof(Tailwind.MSBuild.Tests.AssertionEngineInitializer),
    nameof(Tailwind.MSBuild.Tests.AssertionEngineInitializer.AcknowledgeSoftWarning))]

namespace Tailwind.MSBuild.Tests;

public static class AssertionEngineInitializer
{
    public static void AcknowledgeSoftWarning()
    {
        FluentAssertions.License.Accepted = true;
    }
}
