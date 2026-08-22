using Amazon;
using Amazon.DynamoDBv2;
using Amazon.Runtime.CredentialManagement;
using YamlDotNet.RepresentationModel;

namespace LazyMagic.Service.Test;

// =====================================================================================================
//  DEPENDENCY PROBE for the DynamoDB-backed tests.
//
//  These are INTEGRATION tests: they need a systemconfig naming an AWS profile, credentials for that
//  profile, and LazyMagic's own DynamoDB table (see CallerInfo.DefaultDB in DocumentRepoTests). All
//  three are legitimately absent in most checkouts — LazyMagic is vendored into consuming systems
//  whose AWS accounts contain THEIR tables, not LazyMagic's.
//
//  A missing dependency therefore SKIPS with a reason naming it. It must never FAIL: a red suite that
//  only means "this machine has no LazyMagic AWS account" trains everyone to ignore red, and buries
//  the failures that matter. Before this probe existed, an absent config produced 17 red tests whose
//  message was "systemconfig.yaml not found".
//
//  WHAT THE PROBE DOES NOT DO: it never asserts an operation succeeded. It asks only "can the
//  dependency be reached". A table that EXISTS but returns AccessDenied, or a repo method that
//  returns the wrong answer, still fails — which is the whole point of running these at all.
//
//  WHY A DERIVED ATTRIBUTE AND NOT Assert.Skip: this project is on xunit 2.9.3, where Xunit.Assert
//  has no Skip and FactAttribute has no SkipWhen/SkipUnless — dynamic skip arrived in xunit v3.
//  FactAttribute.Skip is a settable string read at DISCOVERY time, so setting it in a derived
//  attribute's constructor is the supported v2 idiom.
// =====================================================================================================

/// <summary>
/// One-shot probe for the DynamoDB integration dependency. Never throws; failures become a reason.
/// </summary>
public static class DynamoAvailability
{
    private static readonly Lazy<(bool Ok, string? Reason)> Probe = new(Evaluate);

    public static bool IsAvailable => Probe.Value.Ok;
    public static string Reason => Probe.Value.Reason ?? string.Empty;

    /// <summary>The systemconfig the probe resolved, or null. Reused by the fixture so both agree.</summary>
    public static string? ConfigPath { get; private set; }

    /// <summary>AWS profile / region read from that config.</summary>
    public static string? Profile { get; private set; }
    public static string Region { get; private set; } = "us-east-1";

    /// <summary>The table these tests read and write. Must match CallerInfo.DefaultDB.</summary>
    public const string TableName = "lzm_mp";

    private static (bool, string?) Evaluate()
    {
        try
        {
            // lz 0.10.x names configs systemconfig.{systemkey}.{env}.yaml. The unqualified
            // systemconfig.yaml is the ORIGINAL single-file schema and no longer exists in a
            // current workspace, so probe the qualified form FIRST and keep the old one as a
            // fallback for repos still on the old layout.
            ConfigPath = FindUpward("systemconfig.*.yaml") ?? FindUpward("systemconfig.yaml");
            if (ConfigPath is null)
                return (false, "no systemconfig found (looked for systemconfig.*.yaml then " +
                               $"systemconfig.yaml, upward from '{Directory.GetCurrentDirectory()}') — " +
                               "these DynamoDB integration tests need one naming an AWS Profile.");

            using (var reader = new StreamReader(ConfigPath))
            {
                var yaml = new YamlStream();
                yaml.Load(reader);
                if (yaml.Documents.Count == 0 || yaml.Documents[0].RootNode is not YamlMappingNode map)
                    return (false, $"{ConfigPath} is empty or is not a YAML mapping.");

                if (!map.Children.TryGetValue(new YamlScalarNode("Profile"), out var profileNode))
                    return (false, $"'Profile' not found in {ConfigPath}.");
                Profile = ((YamlScalarNode)profileNode).Value;

                if (map.Children.TryGetValue(new YamlScalarNode("Region"), out var regionNode))
                    Region = ((YamlScalarNode)regionNode).Value ?? "us-east-1";
            }

            if (string.IsNullOrWhiteSpace(Profile))
                return (false, $"'Profile' in {ConfigPath} is blank.");

            if (!new CredentialProfileStoreChain().TryGetAWSCredentials(Profile, out var credentials))
                return (false, $"AWS profile '{Profile}' not in the credential store " +
                               $"(try: aws sso login --profile {Profile}).");

            // Reaching the table proves credentials resolve AND the store exists. DescribeTable is
            // the cheapest call that answers both; anything beyond it would be asserting behavior,
            // which is the tests' job, not the probe's.
            using var client = new AmazonDynamoDBClient(credentials, RegionEndpoint.GetBySystemName(Region));
            try
            {
                _ = client.DescribeTableAsync(TableName).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                return (false, $"DynamoDB table '{TableName}' is not reachable with profile " +
                               $"'{Profile}' in {Region} ({ex.GetType().Name}). These tests need " +
                               "LazyMagic's own table; a consuming system's AWS account will not have it.");
            }

            return (true, null);
        }
        catch (Exception ex)
        {
            // A probe must never take the suite down with it.
            return (false, $"DynamoDB availability probe failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static string? FindUpward(string pattern)
    {
        for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir != null; dir = dir.Parent)
        {
            var matches = dir.GetFiles(pattern);
            if (matches.Length > 0)
                return matches.OrderBy(f => f.Name, StringComparer.Ordinal).First().FullName;
        }
        return null;
    }
}

/// <summary>A <see cref="FactAttribute"/> that skips when the DynamoDB dependency is absent.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class DynamoFactAttribute : FactAttribute
{
    public DynamoFactAttribute()
    {
        if (!DynamoAvailability.IsAvailable)
            Skip = DynamoAvailability.Reason;
    }
}
