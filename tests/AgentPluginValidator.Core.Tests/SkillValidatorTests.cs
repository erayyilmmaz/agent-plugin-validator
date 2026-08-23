using AgentPluginValidator.Core.PackageIntake;
using AgentPluginValidator.Core.Validation;

namespace AgentPluginValidator.Core.Tests;

public sealed class SkillValidatorTests
{
    private readonly SkillValidator validator = new();

    [Fact]
    public void Treats_missing_skills_directory_as_absent()
    {
        var result = Validate("absent");
        Assert.Equal(SkillsComponentStatus.Absent, result.Status);
        Assert.Equal(0, result.DiscoveredCount);
    }

    [Fact]
    public void Validates_only_immediate_child_skill_directories_and_reports_partial()
    {
        var result = Validate("mixed");
        Assert.Equal(SkillsComponentStatus.Partial, result.Status);
        Assert.Equal(3, result.DiscoveredCount);
        Assert.Equal(1, result.ValidCount);
        Assert.Equal(2, result.InvalidCount);
        Assert.Contains(result.Findings, finding => finding.RuleId == "APV-SKILL-003");
        Assert.Contains(result.Findings, finding => finding.RuleId == "APV-SKILL-005");
        Assert.DoesNotContain(result.Findings, finding => finding.FilePath.Contains("nested", StringComparison.Ordinal));
    }

    [Fact]
    public void Rejects_unparseable_frontmatter_and_invalid_name_format()
    {
        var result = Validate("invalid");
        Assert.Equal(SkillsComponentStatus.Invalid, result.Status);
        Assert.Contains(result.Findings, finding => finding.RuleId == "APV-SKILL-002");
        Assert.Contains(result.Findings, finding => finding.RuleId == "APV-SKILL-004");
    }

    private SkillsValidationResult Validate(string fixtureName)
    {
        var root = Path.Combine(AppContext.BaseDirectory, "fixtures", "apv5", fixtureName);
        var reader = SafePackageReader.TryCreate(root).Reader!;
        return validator.Validate(reader);
    }
}
