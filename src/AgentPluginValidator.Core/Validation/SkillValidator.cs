using AgentPluginValidator.Core.PackageIntake;

namespace AgentPluginValidator.Core.Validation;

public enum SkillsComponentStatus { Valid, Invalid, Partial, Absent }

public sealed record SkillsValidationResult(SkillsComponentStatus Status, int DiscoveredCount, int ValidCount, int InvalidCount, IReadOnlyList<ValidationFinding> Findings);

public sealed class SkillValidator
{
    public SkillsValidationResult Validate(SafePackageReader reader)
    {
        var skillsRoot = reader.GetContainedDirectory("skills");
        if (!skillsRoot.IsSuccess)
        {
            return skillsRoot.Failure!.Code == PackageReadFailureCode.PathNotFound
                ? new SkillsValidationResult(SkillsComponentStatus.Absent, 0, 0, 0, Array.Empty<ValidationFinding>())
                : new SkillsValidationResult(SkillsComponentStatus.Invalid, 0, 0, 0, new[] { Finding("APV-SKILL-001", "skills could not be accessed as a contained directory.", "Provide a contained skills directory.", "skills") });
        }

        var findings = new List<ValidationFinding>();
        var directories = Directory.EnumerateDirectories(skillsRoot.Value!).OrderBy(Path.GetFileName, StringComparer.Ordinal).ToArray();
        var valid = 0;
        foreach (var directory in directories)
        {
            var directoryName = Path.GetFileName(directory);
            var relativePath = $"skills/{directoryName}/SKILL.md";
            var read = reader.ReadUtf8Text(relativePath);
            if (!read.IsSuccess)
            {
                findings.Add(Finding("APV-SKILL-001", "A discovered skill does not provide a contained regular SKILL.md.", "Add a contained regular SKILL.md to the immediate skill directory.", relativePath));
                continue;
            }

            var frontmatter = ParseFrontmatter(read.Value!);
            if (frontmatter is null)
            {
                findings.Add(Finding("APV-SKILL-002", "SKILL.md does not contain a parseable YAML frontmatter mapping.", "Add --- delimited YAML frontmatter with name and description.", relativePath));
                continue;
            }

            var entryValid = true;
            if (!frontmatter.TryGetValue("name", out var name) || !IsValidName(name))
            {
                findings.Add(Finding("APV-SKILL-004", "Skill name is missing or violates the Agent Skills name format.", "Use a 1–64 character lowercase alphanumeric/hyphen name without leading/trailing or double hyphens.", relativePath));
                entryValid = false;
            }
            else if (!string.Equals(name, directoryName, StringComparison.Ordinal))
            {
                findings.Add(Finding("APV-SKILL-003", "Skill name does not match its immediate parent directory.", "Make the frontmatter name exactly match the skill directory name.", relativePath));
                entryValid = false;
            }

            if (!frontmatter.TryGetValue("description", out var description) || description.Length is < 1 or > 1024)
            {
                findings.Add(Finding("APV-SKILL-005", "Skill description is missing, empty, or longer than 1024 characters.", "Provide a non-empty description no longer than 1024 characters.", relativePath));
                entryValid = false;
            }

            if (entryValid) valid++;
        }

        var discovered = directories.Length;
        var invalid = discovered - valid;
        var status = invalid == 0 ? SkillsComponentStatus.Valid : valid == 0 ? SkillsComponentStatus.Invalid : SkillsComponentStatus.Partial;
        return new SkillsValidationResult(status, discovered, valid, invalid, findings);
    }

    private static ValidationFinding Finding(string ruleId, string explanation, string fix, string path) =>
        ManifestRuleRegistry.Create(ruleId, explanation, fix) with { FilePath = path };

    private static Dictionary<string, string>? ParseFrontmatter(string content)
    {
        var normalized = content.Replace("\r\n", "\n", StringComparison.Ordinal);
        if (!normalized.StartsWith("---\n", StringComparison.Ordinal)) return null;
        var end = normalized.IndexOf("\n---\n", StringComparison.Ordinal);
        if (end < 0) return null;
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in normalized[4..end].Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#')) continue;
            var separator = line.IndexOf(':');
            if (separator <= 0) return null;
            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();
            if (value.Length >= 2 && ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\''))) value = value[1..^1];
            if (key.Length == 0 || values.ContainsKey(key)) return null;
            values[key] = value;
        }
        return values;
    }

    private static bool IsValidName(string value) => value.Length is >= 1 and <= 64 &&
        char.IsAsciiLetterOrDigit(value[0]) && char.IsAsciiLetterOrDigit(value[^1]) &&
        !value.Contains("--", StringComparison.Ordinal) && value.All(c => (c is >= 'a' and <= 'z') || char.IsAsciiDigit(c) || c == '-');
}
