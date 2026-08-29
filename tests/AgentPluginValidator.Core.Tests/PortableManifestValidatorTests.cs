using AgentPluginValidator.Core.PackageIntake;
using AgentPluginValidator.Core.Validation;

namespace AgentPluginValidator.Core.Tests;

public sealed class PortableManifestValidatorTests
{
    private readonly PortableManifestValidator validator = new();

    [Fact]
    public void Accepts_a_complete_portable_manifest_without_component_discovery()
    {
        var result = ValidateFixture("valid");

        Assert.Equal(PackageFormat.PortableAgentPlugins, result.Format);
        Assert.Equal(ValidationStatus.Valid, result.OverallStatus);
        Assert.Equal(ManifestStatus.Valid, result.ManifestStatus);
        Assert.True(result.ComponentDiscoveryAllowed);
        Assert.Empty(result.Findings);
    }

    [Fact]
    public void Reports_and_ignores_unknown_fields_and_non_object_extensions()
    {
        var result = ValidateFixture("warnings");

        Assert.Equal(ValidationStatus.Valid, result.OverallStatus);
        Assert.True(result.ComponentDiscoveryAllowed);
        Assert.Collection(
            result.Findings,
            finding => Assert.Equal("APV-MANIFEST-006", finding.RuleId),
            finding => Assert.Equal("APV-MANIFEST-007", finding.RuleId));
        Assert.All(result.Findings, finding => Assert.Equal(FindingSeverity.Warning, finding.Severity));
    }

    [Theory]
    [InlineData("invalid-json", "APV-MANIFEST-001")]
    [InlineData("invalid-required", "APV-MANIFEST-003")]
    [InlineData("unsupported-schema", "APV-MANIFEST-002")]
    [InlineData("invalid-name-metadata", "APV-MANIFEST-004")]
    public void Rejects_fatal_manifest_failures_and_blocks_component_discovery(string fixtureName, string expectedRuleId)
    {
        var result = ValidateFixture(fixtureName);

        Assert.Equal(PackageFormat.PortableAgentPlugins, result.Format);
        Assert.Equal(ValidationStatus.Invalid, result.OverallStatus);
        Assert.Equal(ManifestStatus.Invalid, result.ManifestStatus);
        Assert.False(result.ComponentDiscoveryAllowed);
        Assert.Contains(result.Findings, finding => finding.RuleId == expectedRuleId && finding.Severity == FindingSeverity.Error);
    }

    [Fact]
    [Theory]
    [InlineData("codex-only", PackageFormat.CodexPlugin)]
    [InlineData("copilot-root", PackageFormat.CopilotPlugin)]
    [InlineData("claude-only", PackageFormat.ClaudePlugin)]
    [InlineData("legacy-openplugin-only", PackageFormat.LegacyOpenPlugin)]
    public void Returns_not_applicable_for_a_recognized_vendor_only_package(
        string fixtureName,
        PackageFormat expectedFormat)
    {
        var result = ValidateFixture(fixtureName);

        Assert.Equal(expectedFormat, result.Format);
        Assert.Equal(ValidationStatus.NotApplicable, result.OverallStatus);
        Assert.Equal(ManifestStatus.NotEvaluated, result.ManifestStatus);
        Assert.False(result.ComponentDiscoveryAllowed);
        var finding = Assert.Single(result.Findings);
        Assert.Equal("APV-FORMAT-001", finding.RuleId);
        Assert.Equal(FindingSeverity.Info, finding.Severity);
        Assert.Equal("APV-POLICY-RESULT-STATUS-V0", finding.SpecificationReference.SourceId);
    }

    [Fact]
    public void Rejects_an_unknown_package_without_a_portable_manifest()
    {
        var result = ValidateFixture("unknown-no-manifest");

        Assert.Equal(PackageFormat.PortableAgentPlugins, result.Format);
        Assert.Equal(ValidationStatus.Invalid, result.OverallStatus);
        Assert.False(result.ComponentDiscoveryAllowed);
        var finding = Assert.Single(result.Findings);
        Assert.Equal("APV-PACKAGE-001", finding.RuleId);
        Assert.Equal("APV-SPEC-PLUGIN-1.0.0", finding.SpecificationReference.SourceId);
    }

    [Fact]
    public void Reports_metadata_shape_errors_without_interpreting_metadata_values()
    {
        var result = ValidateFixture("invalid-name-metadata");

        Assert.Contains(result.Findings, finding => finding.RuleId == "APV-MANIFEST-005");
        Assert.DoesNotContain(result.Findings, finding => finding.Explanation.Contains("SemVer", StringComparison.Ordinal));
    }

    private ManifestValidationResult ValidateFixture(string fixtureName)
    {
        var creation = SafePackageReader.TryCreate(Path.Combine(AppContext.BaseDirectory, "fixtures", "apv4", fixtureName));
        Assert.True(creation.IsSuccess);
        return validator.Validate(creation.Reader!);
    }
}
