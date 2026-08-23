using AgentPluginValidator.Core.PackageIntake;
using AgentPluginValidator.Core.Validation;

return CliApplication.Run(args, Console.Out, Console.Error);

public static class CliApplication
{
    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (!TryParse(args, out var request, out var usageError))
        {
            error.WriteLine($"Input error: {usageError}");
            error.WriteLine("Usage: agent-plugin-validator validate <plugin-directory> [--quiet|--ci]");
            return 2;
        }

        var readerCreation = SafePackageReader.TryCreate(request!.PluginDirectory);
        if (!readerCreation.IsSuccess)
        {
            error.WriteLine($"Input error: {readerCreation.Failure!.Message}");
            return 2;
        }

        var report = new PluginValidator().Validate(readerCreation.Reader!);
        if (request.Mode == OutputMode.Human) RenderHuman(report, output);
        else if (request.Mode == OutputMode.Ci) RenderCi(report, output);
        return ExitCode(report.OverallStatus);
    }

    private static bool TryParse(string[] args, out CliRequest? request, out string? error)
    {
        request = null;
        error = null;
        if (args.Length < 2 || !string.Equals(args[0], "validate", StringComparison.Ordinal))
        {
            error = "Expected the validate command and exactly one plugin directory.";
            return false;
        }

        var mode = OutputMode.Human;
        for (var index = 2; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--quiet" when mode == OutputMode.Human:
                    mode = OutputMode.Quiet;
                    break;
                case "--ci" when mode == OutputMode.Human:
                    mode = OutputMode.Ci;
                    break;
                default:
                    error = $"Unsupported or conflicting option '{args[index]}'.";
                    return false;
            }
        }

        if (string.IsNullOrWhiteSpace(args[1]))
        {
            error = "Plugin directory must not be empty.";
            return false;
        }

        request = new CliRequest(args[1], mode);
        return true;
    }

    private static void RenderHuman(ValidationReport report, TextWriter output)
    {
        output.WriteLine("Agent Plugin Validator");
        output.WriteLine($"Target: {report.Target}");
        output.WriteLine($"Overall: {Status(report.OverallStatus)}");
        foreach (var component in report.Components)
        {
            var entryText = component.EntrySummary is null
                ? string.Empty
                : $" (entries: {component.EntrySummary.ValidCount} valid, {component.EntrySummary.InvalidCount} invalid, {component.EntrySummary.DiscoveredCount} discovered)";
            output.WriteLine($"{ComponentName(component.Kind)}: {Status(component.Status)}{entryText}");
        }
        output.WriteLine($"Findings: Errors: {report.Summary.ErrorCount}, Warnings: {report.Summary.WarningCount}, Info: {report.Summary.InfoCount}");

        foreach (var finding in report.Findings)
        {
            output.WriteLine();
            output.WriteLine($"{Status(finding.Severity)} [{finding.RuleId}] {finding.Component}{Location(finding.FilePath)}");
            output.WriteLine($"  {finding.Explanation}");
            output.WriteLine($"  Fix: {finding.SuggestedFix}");
            output.WriteLine($"  Spec: {finding.SpecificationReference.Title} {finding.SpecificationReference.Locator}");
        }
    }

    private static void RenderCi(ValidationReport report, TextWriter output) => output.WriteLine(
        $"STATUS={Status(report.OverallStatus)} ERRORS={report.Summary.ErrorCount} WARNINGS={report.Summary.WarningCount} INFO={report.Summary.InfoCount} " +
        string.Join(' ', report.Components.Select(component => $"{ComponentName(component.Kind).ToUpperInvariant()}={Status(component.Status)}")));

    private static int ExitCode(ValidationStatus status) => status switch
    {
        ValidationStatus.Valid => 0,
        ValidationStatus.Invalid => 1,
        ValidationStatus.NotApplicable => 3,
        _ => 2
    };

    private static string Status(ValidationStatus status) => status switch
    {
        ValidationStatus.Valid => "VALID",
        ValidationStatus.Invalid => "INVALID",
        ValidationStatus.NotApplicable => "NOT_APPLICABLE",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };

    private static string Status(ComponentStatus status) => status switch
    {
        ComponentStatus.Valid => "VALID",
        ComponentStatus.Invalid => "INVALID",
        ComponentStatus.Partial => "PARTIAL",
        ComponentStatus.NotEvaluated => "NOT_EVALUATED",
        ComponentStatus.Absent => "ABSENT",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };

    private static string Status(FindingSeverity severity) => severity.ToString().ToUpperInvariant();
    private static string ComponentName(ComponentKind kind) => kind == ComponentKind.Mcp ? "MCP" : kind.ToString();
    private static string Location(string path) => string.IsNullOrEmpty(path) ? string.Empty : $" ({path})";

    private sealed record CliRequest(string PluginDirectory, OutputMode Mode);
    private enum OutputMode { Human, Quiet, Ci }
}
