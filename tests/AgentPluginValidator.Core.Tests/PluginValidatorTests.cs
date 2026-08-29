using AgentPluginValidator.Core.PackageIntake;
using AgentPluginValidator.Core.Validation;

namespace AgentPluginValidator.Core.Tests;

public sealed class PluginValidatorTests
{
    private readonly PluginValidator validator = new();

    [Fact]
    public void Aggregates_valid_manifest_and_absent_optional_components()
    {
        var report = ValidateFixture("apv6", "absent");

        Assert.Equal(ValidationStatus.Valid, report.OverallStatus);
        Assert.Equal([ComponentStatus.Valid, ComponentStatus.Absent, ComponentStatus.Absent], report.Components.Select(component => component.Status));
        Assert.Equal(0, report.Summary.ErrorCount);
    }

    [Fact]
    public void Preserves_component_local_failures_and_marks_the_package_invalid()
    {
        var report = ValidateFixture("apv6", "mixed");

        Assert.Equal(ValidationStatus.Invalid, report.OverallStatus);
        Assert.Equal(ComponentStatus.Absent, report.Components[1].Status);
        Assert.Equal(ComponentStatus.Partial, report.Components[2].Status);
        Assert.Equal(9, report.Components[2].EntrySummary!.DiscoveredCount);
        Assert.True(report.Summary.ErrorCount > 0);
    }

    [Fact]
    public void Does_not_discover_optional_components_after_a_fatal_manifest_error()
    {
        var report = ValidateFixture("apv4", "invalid-required");

        Assert.Equal(ValidationStatus.Invalid, report.OverallStatus);
        Assert.Equal(ComponentStatus.Invalid, report.Components[0].Status);
        Assert.All(report.Components.Skip(1), component => Assert.Equal(ComponentStatus.NotEvaluated, component.Status));
    }

    [Fact]
    [Theory]
    [InlineData("codex-only")]
    [InlineData("copilot-root")]
    [InlineData("claude-only")]
    [InlineData("legacy-openplugin-only")]
    public void Reports_recognized_vendor_only_packages_as_not_applicable(string fixture)
    {
        var report = ValidateFixture("apv4", fixture);

        Assert.Equal(ValidationStatus.NotApplicable, report.OverallStatus);
        Assert.All(report.Components, component => Assert.Equal(ComponentStatus.NotEvaluated, component.Status));
        Assert.Equal(1, report.Summary.InfoCount);
    }

    private ValidationReport ValidateFixture(string task, string fixture)
    {
        var creation = SafePackageReader.TryCreate(Path.Combine(AppContext.BaseDirectory, "fixtures", task, fixture));
        Assert.True(creation.IsSuccess);
        return validator.Validate(creation.Reader!);
    }
}
