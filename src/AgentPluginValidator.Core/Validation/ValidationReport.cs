using AgentPluginValidator.Core.PackageIntake;

namespace AgentPluginValidator.Core.Validation;

public enum ComponentKind { Manifest, Skills, Mcp }

public enum ComponentStatus { Valid, Invalid, Partial, NotEvaluated, Absent }

public sealed record EntrySummary(int DiscoveredCount, int ValidCount, int InvalidCount);

public sealed record ComponentResult(
    ComponentKind Kind,
    ComponentStatus Status,
    EntrySummary? EntrySummary,
    IReadOnlyList<int> FindingIndexes);

public sealed record ValidationSummary(
    int ErrorCount,
    int WarningCount,
    int InfoCount,
    int ValidComponentCount,
    int InvalidComponentCount,
    int PartialComponentCount,
    int NotEvaluatedComponentCount,
    int AbsentComponentCount);

public sealed record ValidationReport(
    string ReportContractVersion,
    string Target,
    PackageFormat Format,
    ValidationStatus OverallStatus,
    IReadOnlyList<ComponentResult> Components,
    IReadOnlyList<ValidationFinding> Findings,
    ValidationSummary Summary);

/// <summary>
/// Transport-neutral composition of the static Core validators. It never parses
/// command-line arguments, writes output, executes package content, or connects
/// to MCP endpoints.
/// </summary>
public sealed class PluginValidator
{
    private readonly PortableManifestValidator manifestValidator = new();
    private readonly SkillValidator skillValidator = new();
    private readonly McpValidator mcpValidator = new();

    public ValidationReport Validate(SafePackageReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        var manifest = manifestValidator.Validate(reader);
        SkillsValidationResult? skills = null;
        McpValidationResult? mcp = null;
        if (manifest.ComponentDiscoveryAllowed)
        {
            skills = skillValidator.Validate(reader);
            mcp = mcpValidator.Validate(reader, manifest);
        }

        var findings = OrderFindings(manifest.Findings, skills?.Findings, mcp?.Findings);
        var components = new[]
        {
            Component(ComponentKind.Manifest, ToComponentStatus(manifest.ManifestStatus), null, findings),
            Component(ComponentKind.Skills, skills is null ? ComponentStatus.NotEvaluated : ToComponentStatus(skills.Status), skills is null ? null : new EntrySummary(skills.DiscoveredCount, skills.ValidCount, skills.InvalidCount), findings),
            Component(ComponentKind.Mcp, mcp is null ? ComponentStatus.NotEvaluated : ToComponentStatus(mcp.Status), mcp is null ? null : new EntrySummary(mcp.DiscoveredCount, mcp.ValidCount, mcp.InvalidCount), findings)
        };
        var overall = manifest.OverallStatus == ValidationStatus.NotApplicable
            ? ValidationStatus.NotApplicable
            : findings.Any(finding => finding.Severity == FindingSeverity.Error) ? ValidationStatus.Invalid : ValidationStatus.Valid;

        return new ValidationReport(
            "1.0",
            Path.GetFileName(reader.ResolvedRootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
            manifest.Format,
            overall,
            components,
            findings,
            Summary(findings, components));
    }

    private static ComponentResult Component(ComponentKind kind, ComponentStatus status, EntrySummary? summary, IReadOnlyList<ValidationFinding> findings) => new(
        kind,
        status,
        summary,
        findings.Select((finding, index) => (finding, index))
            .Where(item => Owns(kind, item.finding.Component))
            .Select(item => item.index)
            .ToArray());

    private static bool Owns(ComponentKind kind, FindingComponent component) => kind switch
    {
        ComponentKind.Manifest => component is FindingComponent.Package or FindingComponent.Manifest,
        ComponentKind.Skills => component is FindingComponent.Skills or FindingComponent.Skill,
        ComponentKind.Mcp => component is FindingComponent.Mcp or FindingComponent.McpServer,
        _ => false
    };

    private static IReadOnlyList<ValidationFinding> OrderFindings(params IReadOnlyList<ValidationFinding>?[] collections) => collections
        .Where(collection => collection is not null)
        .SelectMany(collection => collection!)
        .OrderBy(finding => ComponentOrder(finding.Component))
        .ThenBy(finding => finding.RuleId, StringComparer.Ordinal)
        .ThenBy(finding => finding.FilePath, StringComparer.Ordinal)
        .ToArray();

    private static int ComponentOrder(FindingComponent component) => component switch
    {
        FindingComponent.Package => 0,
        FindingComponent.Manifest => 1,
        FindingComponent.Skills => 2,
        FindingComponent.Skill => 3,
        FindingComponent.Mcp => 4,
        FindingComponent.McpServer => 5,
        _ => int.MaxValue
    };

    private static ComponentStatus ToComponentStatus(ManifestStatus status) => status switch
    {
        ManifestStatus.Valid => ComponentStatus.Valid,
        ManifestStatus.Invalid => ComponentStatus.Invalid,
        _ => ComponentStatus.NotEvaluated
    };

    private static ComponentStatus ToComponentStatus(SkillsComponentStatus status) => status switch
    {
        SkillsComponentStatus.Valid => ComponentStatus.Valid,
        SkillsComponentStatus.Invalid => ComponentStatus.Invalid,
        SkillsComponentStatus.Partial => ComponentStatus.Partial,
        _ => ComponentStatus.Absent
    };

    private static ComponentStatus ToComponentStatus(McpComponentStatus status) => status switch
    {
        McpComponentStatus.Valid => ComponentStatus.Valid,
        McpComponentStatus.Invalid => ComponentStatus.Invalid,
        McpComponentStatus.Partial => ComponentStatus.Partial,
        McpComponentStatus.NotEvaluated => ComponentStatus.NotEvaluated,
        _ => ComponentStatus.Absent
    };

    private static ValidationSummary Summary(IReadOnlyList<ValidationFinding> findings, IReadOnlyList<ComponentResult> components) => new(
        findings.Count(finding => finding.Severity == FindingSeverity.Error),
        findings.Count(finding => finding.Severity == FindingSeverity.Warning),
        findings.Count(finding => finding.Severity == FindingSeverity.Info),
        components.Count(component => component.Status == ComponentStatus.Valid),
        components.Count(component => component.Status == ComponentStatus.Invalid),
        components.Count(component => component.Status == ComponentStatus.Partial),
        components.Count(component => component.Status == ComponentStatus.NotEvaluated),
        components.Count(component => component.Status == ComponentStatus.Absent));
}
