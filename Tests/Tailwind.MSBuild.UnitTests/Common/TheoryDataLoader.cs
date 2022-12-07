namespace Tailwind.MSBuild.UnitTests.Common;

using System.Reflection;
using Tailwind.MSBuild.GitHub;
using Xunit;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public class TheoryDataAttribute : MemberDataAttributeBase
{
    public TheoryDataAttribute(params string[] parameters) : base(nameof(TheoryDataLoader.LoadDataFor), parameters)
    {
        this.MemberType = typeof(TheoryDataLoader);
    }

    protected override object[] ConvertDataItem(MethodInfo testMethod, object item)
    {
        if (item is null)
            return null!;

        if (item is not object[] array)
            throw new ArgumentException($"Property {this.MemberName} on {this.MemberType ?? testMethod.DeclaringType} yielded an item that is not an object[]");

        return array;
    }
}

public static class TheoryDataLoader
{
    public static IEnumerable<object[]> LoadDataFor(string[] parameters)
    {
        var result = new List<object[]>();

        foreach (var param in parameters)
        {
            // Map param to some data
        }

        return result;
    }

    private static T LoadFromJson<T>() where T : class
    {
        var result = new List<object[]>();
        var src = Path.Combine(Environment.CurrentDirectory, "..", "TheoryData", typeof(T).Name, ".json");

        if (!File.Exists(src))
            throw new FileNotFoundException(src);

        if (JsonSerializer.Deserialize<T>(src) is not T obj)
            throw new InvalidOperationException($"Unable to deserialize theory data from file \"{src}\"");

        return obj;
    }
}
