using AgentPluginValidator.Core.PackageIntake;
using AgentPluginValidator.Core.Validation;

namespace AgentPluginValidator.Core.Tests;

public sealed class RegressionFixtureTests
{
    private readonly PluginValidator validator = new();

    [Theory]
    [InlineData("minimal-valid", ValidationStatus.Valid, ComponentStatus.Valid, ComponentStatus.Absent, ComponentStatus.Absent, null)]
    [InlineData("full-valid", ValidationStatus.Valid, ComponentStatus.Valid, ComponentStatus.Valid, ComponentStatus.Valid, null)]
    [InlineData("client-extensions", ValidationStatus.Valid, ComponentStatus.Valid, ComponentStatus.Absent, ComponentStatus.Absent, null)]
    [InlineData("invalid-manifest", ValidationStatus.Invalid, ComponentStatus.Invalid, ComponentStatus.NotEvaluated, ComponentStatus.NotEvaluated, "APV-MANIFEST-004")]
    [InlineData("invalid-skill", ValidationStatus.Invalid, ComponentStatus.Valid, ComponentStatus.Invalid, ComponentStatus.Valid, "APV-SKILL-004")]
    [InlineData("invalid-mcp", ValidationStatus.Invalid, ComponentStatus.Valid, ComponentStatus.Valid, ComponentStatus.Invalid, "APV-MCP-001")]
    [InlineData("version-mismatch", ValidationStatus.Invalid, ComponentStatus.Valid, ComponentStatus.Absent, ComponentStatus.Invalid, "APV-CROSS-001")]
    [InlineData("path-traversal", ValidationStatus.Invalid, ComponentStatus.Valid, ComponentStatus.Absent, ComponentStatus.Invalid, "APV-MCP-011")]
    [InlineData("secret-header", ValidationStatus.Invalid, ComponentStatus.Valid, ComponentStatus.Absent, ComponentStatus.Invalid, "APV-MCP-022")]
    public void Validates_the_regression_fixture_inventory_with_the_expected_boundary(
        string fixture,
        ValidationStatus overall,
        ComponentStatus manifest,
        ComponentStatus skills,
        ComponentStatus mcp,
        string? expectedRuleId)
    {
        var report = Validate(fixture);

        Assert.Equal(overall, report.OverallStatus);
        Assert.Equal([manifest, skills, mcp], report.Components.Select(component => component.Status));
        if (expectedRuleId is null) Assert.Empty(report.Findings);
        else Assert.Contains(report.Findings, finding => finding.RuleId == expectedRuleId);
    }

    [Fact]
    public void Produces_the_same_complete_report_signature_for_repeated_fixture_validation()
    {
        var expected = Signature(Validate("full-valid"));

        for (var attempt = 0; attempt < 5; attempt++)
        {
            Assert.Equal(expected, Signature(Validate("full-valid")));
            Assert.Equal(Signature(Validate("secret-header")), Signature(Validate("secret-header")));
        }
    }

    [Fact]
    public void Covers_the_remaining_individual_mcp_rule_failures_with_the_existing_mixed_fixture()
    {
        var report = ValidateExisting("apv6", "mixed");
        var expectedRules = new[]
        {
            "APV-MCP-003", "APV-MCP-010", "APV-MCP-011", "APV-MCP-012", "APV-MCP-013",
            "APV-MCP-020", "APV-MCP-021", "APV-MCP-022", "APV-MCP-023"
        };

        Assert.Equal(ComponentStatus.Partial, report.Components.Single(component => component.Kind == ComponentKind.Mcp).Status);
        Assert.Equal(expectedRules, report.Findings.Where(finding => expectedRules.Contains(finding.RuleId)).Select(finding => finding.RuleId).Distinct().OrderBy(rule => rule));
    }

    [Fact]
    public void Keeps_regression_fixtures_inert_and_non_executable()
    {
        if (OperatingSystem.IsWindows()) return;

        foreach (var file in Directory.GetFiles(FixtureRoot(), "*", SearchOption.AllDirectories))
        {
            var mode = File.GetUnixFileMode(file);
            Assert.Equal(UnixFileMode.None, mode & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute));
        }
    }

    [Fact]
    public void Ignores_client_extension_namespaces_without_reading_or_interpreting_their_contents()
    {
        var root = Path.Combine(FixtureRoot(), "client-extensions");
        var creation = SafePackageReader.TryCreate(root);
        Assert.True(creation.IsSuccess);
        var reader = Assert.IsType<SafePackageReader>(creation.Reader);

        var report = validator.Validate(reader);

        Assert.Equal(ValidationStatus.Valid, report.OverallStatus);
        Assert.Empty(report.Findings);
        Assert.Equal(
            new FileInfo(Path.Combine(root, "plugin.json")).Length,
            reader.TotalBytesRead);
    }

    private ValidationReport Validate(string fixture) => ValidateAt(Path.Combine(FixtureRoot(), fixture));

    private ValidationReport ValidateExisting(string task, string fixture) => ValidateAt(Path.Combine(AppContext.BaseDirectory, "fixtures", task, fixture));

    private ValidationReport ValidateAt(string root)
    {
        var creation = SafePackageReader.TryCreate(root);
        Assert.True(creation.IsSuccess);
        return validator.Validate(creation.Reader!);
    }

    private static string FixtureRoot() => Path.Combine(AppContext.BaseDirectory, "fixtures", "apv8");

    private static string Signature(ValidationReport report) => string.Join("\n", new[]
    {
        report.OverallStatus.ToString(),
        string.Join("|", report.Components.Select(component => $"{component.Kind}:{component.Status}:{component.EntrySummary?.DiscoveredCount}:{component.EntrySummary?.ValidCount}:{component.EntrySummary?.InvalidCount}")),
        string.Join("|", report.Findings.Select(finding => $"{finding.RuleId}:{finding.Severity}:{finding.Component}:{finding.FilePath}:{finding.SpecificationReference.SourceId}")),
        $"{report.Summary.ErrorCount}:{report.Summary.WarningCount}:{report.Summary.InfoCount}"
    });
}
