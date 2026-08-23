using AgentPluginValidator.Core.PackageIntake;
using AgentPluginValidator.Core.Validation;

namespace AgentPluginValidator.Core.Tests;

public sealed class McpValidatorTests
{
    private readonly PortableManifestValidator manifestValidator = new();
    private readonly McpValidator validator = new();

    [Fact]
    public void Treats_a_missing_optional_mcp_configuration_as_absent()
    {
        var result = ValidateFixture("absent");

        Assert.Equal(McpComponentStatus.Absent, result.Status);
        Assert.Equal(0, result.DiscoveredCount);
        Assert.Empty(result.Findings);
    }

    [Theory]
    [InlineData("invalid-top", "APV-MCP-001")]
    [InlineData("version-mismatch", "APV-CROSS-001")]
    public void Disables_only_the_mcp_component_for_invalid_top_level_configuration(string fixtureName, string expectedRuleId)
    {
        var result = ValidateFixture(fixtureName);

        Assert.Equal(McpComponentStatus.Invalid, result.Status);
        Assert.Equal(0, result.DiscoveredCount);
        Assert.Contains(result.Findings, finding => finding.RuleId == expectedRuleId && finding.Component == FindingComponent.Mcp);
        if (fixtureName == "version-mismatch") Assert.Contains(result.Findings, finding => finding.RuleId == "APV-MCP-002");
    }

    [Fact]
    public void Accepts_all_supported_static_transport_variants()
    {
        var result = ValidateFixture("valid");

        Assert.Equal(McpComponentStatus.Valid, result.Status);
        Assert.Equal(3, result.DiscoveredCount);
        Assert.Equal(3, result.ValidCount);
        Assert.Empty(result.Findings);
    }

    [Fact]
    public void Preserves_valid_servers_when_individual_servers_violate_transport_and_security_rules()
    {
        var result = ValidateFixture("mixed");

        Assert.Equal(McpComponentStatus.Partial, result.Status);
        Assert.Equal(9, result.DiscoveredCount);
        Assert.Equal(1, result.ValidCount);
        Assert.Equal(8, result.InvalidCount);
        foreach (var ruleId in new[] { "APV-MCP-003", "APV-MCP-010", "APV-MCP-011", "APV-MCP-012", "APV-MCP-013", "APV-MCP-020", "APV-MCP-021", "APV-MCP-022", "APV-MCP-023" })
        {
            Assert.Contains(result.Findings, finding => finding.RuleId == ruleId && finding.Component == FindingComponent.McpServer);
        }
        Assert.All(result.Findings, finding => Assert.Equal("APV-SPEC-PLUGIN-1.0.0", finding.SpecificationReference.SourceId));
    }

    [Fact]
    public void Does_not_read_mcp_when_manifest_has_already_blocked_component_discovery()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "fixtures", "apv4", "invalid-required");
        var reader = SafePackageReader.TryCreate(root).Reader!;
        var manifest = manifestValidator.Validate(reader);

        var result = validator.Validate(reader, manifest);

        Assert.Equal(McpComponentStatus.NotEvaluated, result.Status);
        Assert.Empty(result.Findings);
    }

    private McpValidationResult ValidateFixture(string fixtureName)
    {
        var creation = SafePackageReader.TryCreate(Path.Combine(AppContext.BaseDirectory, "fixtures", "apv6", fixtureName));
        Assert.True(creation.IsSuccess);
        var manifest = manifestValidator.Validate(creation.Reader!);
        Assert.True(manifest.ComponentDiscoveryAllowed);
        return validator.Validate(creation.Reader!, manifest);
    }
}
