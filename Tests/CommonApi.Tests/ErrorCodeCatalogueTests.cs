using System.Reflection;
using smart_pet_care_api.Common.Api;
using Xunit;

namespace smart_pet_care_api.Common.Api.Tests;

/// <summary>
/// The catalogue is a published contract: the frontend keys its translations off
/// it. These two checks are what stop it from drifting out of the code silently.
/// </summary>
public class ErrorCodeCatalogueTests
{
    [Fact]
    public void EveryAliasIsUnique()
    {
        var duplicates = AllCodes()
            .GroupBy(entry => entry.Code)
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key} ({string.Join(", ", group.Select(entry => entry.Name))})")
            .ToList();

        Assert.Empty(duplicates);
    }

    [Fact]
    public void EveryAliasIsDocumented()
    {
        var document = File.ReadAllText(DocumentPath());

        var undocumented = AllCodes()
            .Where(entry => !document.Contains($"`{entry.Code}`", StringComparison.Ordinal))
            .Select(entry => entry.Name)
            .ToList();

        Assert.Empty(undocumented);
    }

    private static IEnumerable<(string Name, string Code)> AllCodes()
    {
        foreach (var entry in CodesOf(typeof(ErrorCodes)))
            yield return entry;

        foreach (var group in typeof(ErrorCodes).GetNestedTypes(BindingFlags.Public))
            foreach (var entry in CodesOf(group))
                yield return entry;
    }

    private static IEnumerable<(string Name, string Code)> CodesOf(Type type) =>
        type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(field => field is { IsLiteral: true, IsInitOnly: false }
                && field.FieldType == typeof(string))
            .Select(field => ($"{type.Name}.{field.Name}", (string)field.GetRawConstantValue()!));

    private static string DocumentPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "docs", "error-codes.md");
            if (File.Exists(candidate))
                return candidate;

            directory = directory.Parent;
        }

        throw new FileNotFoundException("docs/error-codes.md was not found above the test output.");
    }
}
