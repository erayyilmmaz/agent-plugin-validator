namespace AgentPluginValidator.Core.Validation;

public enum ValidationStatus
{
    Valid,
    Invalid,
    NotApplicable
}

public enum ManifestStatus
{
    Valid,
    Invalid,
    NotEvaluated
}

public enum FindingSeverity
{
    Error,
    Warning,
    Info
}

public enum FindingComponent
{
    Package,
    Manifest,
    Skills,
    Skill
}

public enum PackageFormat
{
    PortableAgentPlugins,
    CodexPlugin,
    Unknown
}

public sealed record SpecificationReference(
    string SourceId,
    string Title,
    string VersionOrSnapshot,
    string Locator,
    string CanonicalLocator);

public sealed record ValidationFinding(
    string RuleId,
    FindingSeverity Severity,
    FindingComponent Component,
    string FilePath,
    string Explanation,
    string SuggestedFix,
    SpecificationReference SpecificationReference);

public sealed record ManifestValidationResult(
    PackageFormat Format,
    ValidationStatus OverallStatus,
    ManifestStatus ManifestStatus,
    bool ComponentDiscoveryAllowed,
    IReadOnlyList<ValidationFinding> Findings);
