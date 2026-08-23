namespace AgentPluginValidator.Core.Validation;

internal static class ManifestRuleRegistry
{
    private const string PluginSpecUrl = "https://github.com/agentplugins/agent-plugins-spec/blob/main/spec/1.0.0.md";
    private const string PolicyUrl = "apv://policy/result-status-contract";

    public static ValidationFinding Create(string ruleId, string explanation, string suggestedFix) =>
        ruleId switch
        {
            "APV-PACKAGE-001" => Finding(ruleId, FindingSeverity.Error, FindingComponent.Package, "§4.1.1–2; §5.1", explanation, suggestedFix),
            "APV-PATH-001" => Finding(ruleId, FindingSeverity.Error, FindingComponent.Package, "§4.1.3", explanation, suggestedFix),
            "APV-MANIFEST-001" => Finding(ruleId, FindingSeverity.Error, FindingComponent.Manifest, "§5.2", explanation, suggestedFix),
            "APV-MANIFEST-002" => Finding(ruleId, FindingSeverity.Error, FindingComponent.Manifest, "§5.2–§5.3", explanation, suggestedFix),
            "APV-MANIFEST-003" => Finding(ruleId, FindingSeverity.Error, FindingComponent.Manifest, "§5.3", explanation, suggestedFix),
            "APV-MANIFEST-004" => Finding(ruleId, FindingSeverity.Error, FindingComponent.Manifest, "§5.5", explanation, suggestedFix),
            "APV-MANIFEST-005" => Finding(ruleId, FindingSeverity.Error, FindingComponent.Manifest, "§5.2; §5.4", explanation, suggestedFix),
            "APV-MANIFEST-006" => Finding(ruleId, FindingSeverity.Warning, FindingComponent.Manifest, "§5.2", explanation, suggestedFix),
            "APV-MANIFEST-007" => Finding(ruleId, FindingSeverity.Warning, FindingComponent.Manifest, "§5.2; §8.1", explanation, suggestedFix),
            "APV-FORMAT-001" => new ValidationFinding(
                ruleId,
                FindingSeverity.Info,
                FindingComponent.Package,
                "",
                explanation,
                suggestedFix,
                new SpecificationReference(
                    "APV-POLICY-RESULT-STATUS-V0",
                    "APV Failure-Boundary and Result-Status Contract",
                    "V0",
                    "Applicability and initial decision tree",
                    PolicyUrl)),
            "APV-SKILL-001" => SkillFinding(ruleId, FindingSeverity.Error, "Agent Plugins Specification", "1.0.0", "§4.1; §7.1", PluginSpecUrl, explanation, suggestedFix),
            "APV-SKILL-002" => SkillFinding(ruleId, FindingSeverity.Error, "Agent Skills Specification", "checked 2026-08-23", "SKILL.md format", "https://agentskills.io/specification", explanation, suggestedFix),
            "APV-SKILL-003" => SkillFinding(ruleId, FindingSeverity.Error, "Agent Skills Specification", "checked 2026-08-23", "name field", "https://agentskills.io/specification", explanation, suggestedFix),
            "APV-SKILL-004" => SkillFinding(ruleId, FindingSeverity.Error, "Agent Skills Specification", "checked 2026-08-23", "frontmatter, name field", "https://agentskills.io/specification", explanation, suggestedFix),
            "APV-SKILL-005" => SkillFinding(ruleId, FindingSeverity.Error, "Agent Skills Specification", "checked 2026-08-23", "frontmatter, description field", "https://agentskills.io/specification", explanation, suggestedFix),
            _ => throw new ArgumentOutOfRangeException(nameof(ruleId), ruleId, "Unknown manifest rule ID.")
        };

    private static ValidationFinding Finding(
        string ruleId,
        FindingSeverity severity,
        FindingComponent component,
        string locator,
        string explanation,
        string suggestedFix) => new(
            ruleId,
            severity,
            component,
            "plugin.json",
            explanation,
            suggestedFix,
            new SpecificationReference(
                "APV-SPEC-PLUGIN-1.0.0",
                "Agent Plugins Specification",
                "1.0.0",
                locator,
                PluginSpecUrl));

    private static ValidationFinding SkillFinding(string ruleId, FindingSeverity severity, string title, string version, string locator, string url, string explanation, string suggestedFix) => new(
        ruleId, severity, FindingComponent.Skill, "", explanation, suggestedFix,
        new SpecificationReference(title == "Agent Plugins Specification" ? "APV-SPEC-PLUGIN-1.0.0" : "APV-SPEC-SKILLS", title, version, locator, url));
}
