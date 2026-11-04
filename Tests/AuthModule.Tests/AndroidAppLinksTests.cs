using System.Text.Json;
using Xunit;

namespace smart_pet_care_api.Modules.AuthModule.Tests;

/// <summary>
/// <c>nginx/assetlinks.json</c> is what makes the reset link safe: Android opens
/// https://host/reset-password in the app only while this file says the app owns the
/// domain, and falls back to a browser the moment it stops parsing or stops matching
/// the signing key. Nothing fails loudly when that happens — the link simply opens a
/// page again — so the shape is pinned here.
/// </summary>
public class AndroidAppLinksTests
{
    private const string Package = "com.misopets.app";
    private const string Relation = "delegate_permission/common.handle_all_urls";

    [Fact]
    public void AssetLinks_DelegatesEveryUrlToTheAndroidApp()
    {
        var statement = Assert.Single(Statements());

        Assert.Equal(Relation, Assert.Single(statement.GetProperty("relation").EnumerateArray()).GetString());

        var target = statement.GetProperty("target");
        Assert.Equal("android_app", target.GetProperty("namespace").GetString());
        Assert.Equal(Package, target.GetProperty("package_name").GetString());
    }

    /// <summary>
    /// A SHA-1 fingerprint pasted in by mistake is 20 pairs rather than 32, and a
    /// truncated paste is fewer still. Either leaves verification failing silently.
    /// </summary>
    [Fact]
    public void AssetLinks_CarriesAtLeastOneFullSha256Fingerprint()
    {
        var fingerprints = Assert.Single(Statements())
            .GetProperty("target")
            .GetProperty("sha256_cert_fingerprints")
            .EnumerateArray()
            .Select(value => value.GetString())
            .ToList();

        Assert.NotEmpty(fingerprints);

        foreach (var fingerprint in fingerprints)
        {
            var octets = fingerprint!.Split(':');
            Assert.Equal(32, octets.Length);
            Assert.All(octets, octet => Assert.True(
                octet.Length == 2 && octet.All(Uri.IsHexDigit),
                $"'{octet}' is not a hex octet in '{fingerprint}'."));
        }
    }

    private static List<JsonElement> Statements()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(AssetLinksPath()));
        return document.RootElement.EnumerateArray().Select(element => element.Clone()).ToList();
    }

    /// <summary>
    /// Walked up from the test output the way PasswordResetTemplateTests finds
    /// <c>Pages/</c>. The file is served by nginx from a mount, so there is no copy in
    /// the publish output to read instead.
    /// </summary>
    private static string AssetLinksPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "nginx", "assetlinks.json");
            if (File.Exists(candidate)) return candidate;

            directory = directory.Parent;
        }

        throw new FileNotFoundException("nginx/assetlinks.json was not found above the test output.");
    }
}
