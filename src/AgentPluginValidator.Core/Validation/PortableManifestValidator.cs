using System.Text.Json;
using AgentPluginValidator.Core.PackageIntake;

namespace AgentPluginValidator.Core.Validation;

public sealed class PortableManifestValidator
{
    public const string CanonicalPluginSchema = "https://agent-plugins.org/schemas/1.0.0/plugin.schema.json";

    private static readonly HashSet<string> PermittedFields = new(StringComparer.Ordinal)
    {
        "$schema", "name", "version", "description", "author", "homepage", "repository", "license", "keywords", "extensions"
    };

    private static readonly HashSet<string> AuthorFields = new(StringComparer.Ordinal)
    {
        "name", "email", "url"
    };

    public ManifestValidationResult Validate(SafePackageReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        var manifestRead = reader.ReadUtf8Text("plugin.json");
        if (!manifestRead.IsSuccess)
        {
            return ValidateMissingOrUnreadableManifest(reader, manifestRead.Failure!);
        }

        var findings = new List<ValidationFinding>();
        try
        {
            using var document = JsonDocument.Parse(manifestRead.Value!);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                findings.Add(ManifestRuleRegistry.Create(
                    "APV-MANIFEST-001",
                    "Root plugin.json must contain a JSON object.",
                    "Replace plugin.json with a top-level JSON object."));
                return Fatal(findings);
            }

            var manifest = document.RootElement;
            if (!manifest.TryGetProperty("$schema", out _))
            {
                return NotApplicable(
                    PackageFormat.CopilotPlugin,
                    "A Copilot-format root plugin.json was found without the canonical portable Agent Plugins schema.");
            }

            ValidateUnknownFields(manifest, findings);
            ValidateSchema(manifest, findings);
            ValidateName(manifest, findings);
            ValidateMetadata(manifest, findings);
            ValidateExtensions(manifest, findings);
        }
        catch (JsonException)
        {
            findings.Add(ManifestRuleRegistry.Create(
                "APV-MANIFEST-001",
                "Root plugin.json is not valid JSON.",
                "Fix plugin.json so it is valid JSON with a top-level object."));
            return Fatal(findings);
        }

        return findings.Any(finding => finding.Severity == FindingSeverity.Error)
            ? Fatal(findings)
            : new ManifestValidationResult(
                PackageFormat.PortableAgentPlugins,
                ValidationStatus.Valid,
                ManifestStatus.Valid,
                ComponentDiscoveryAllowed: true,
                findings);
    }

    private static ManifestValidationResult ValidateMissingOrUnreadableManifest(
        SafePackageReader reader,
        PackageReadFailure failure)
    {
        if (failure.Code == PackageReadFailureCode.PathNotFound && TryDetectVendorFormat(reader, out var format, out var explanation))
        {
            return NotApplicable(format, explanation);
        }

        var ruleId = failure.Code is PackageReadFailureCode.SymlinkEscapesRoot or PackageReadFailureCode.PathEscapesRoot
            ? "APV-PATH-001"
            : "APV-PACKAGE-001";
        var explanation = ruleId == "APV-PATH-001"
            ? "Root plugin.json could not be read because its resolved path is outside the plugin root."
            : "A readable root plugin.json is required for portable Agent Plugins validation.";

        return Fatal(new[]
        {
            ManifestRuleRegistry.Create(
                ruleId,
                explanation,
                "Provide a contained, readable root plugin.json without changing the plugin root boundary.")
        });
    }

    private static bool TryDetectVendorFormat(
        SafePackageReader reader,
        out PackageFormat format,
        out string explanation)
    {
        foreach (var marker in VendorMarkers)
        {
            if (!reader.ReadUtf8Text(marker.Path).IsSuccess)
            {
                continue;
            }

            format = marker.Format;
            explanation = marker.Explanation;
            return true;
        }

        format = PackageFormat.Unknown;
        explanation = string.Empty;
        return false;
    }

    private static ManifestValidationResult NotApplicable(PackageFormat format, string explanation) => new(
        format,
        ValidationStatus.NotApplicable,
        ManifestStatus.NotEvaluated,
        ComponentDiscoveryAllowed: false,
        new[]
        {
            ManifestRuleRegistry.Create(
                "APV-FORMAT-001",
                explanation,
                "Add a portable Agent Plugins 1.0.0 root plugin.json to request portable conformance validation.")
        });

    private static readonly VendorMarker[] VendorMarkers =
    {
        new(".codex-plugin/plugin.json", PackageFormat.CodexPlugin, "A Codex-specific plugin manifest was found without a portable root plugin.json."),
        new(".claude-plugin/plugin.json", PackageFormat.ClaudePlugin, "A Claude-format plugin manifest was found without a portable root plugin.json."),
        new(".plugin/plugin.json", PackageFormat.LegacyOpenPlugin, "A legacy OpenPlugin manifest was found without a portable root plugin.json.")
    };

    private sealed record VendorMarker(string Path, PackageFormat Format, string Explanation);

    private static void ValidateUnknownFields(JsonElement manifest, ICollection<ValidationFinding> findings)
    {
        foreach (var property in manifest.EnumerateObject())
        {
            if (!PermittedFields.Contains(property.Name))
            {
                findings.Add(ManifestRuleRegistry.Create(
                    "APV-MANIFEST-006",
                    $"Top-level field '{property.Name}' is unknown and was ignored.",
                    "Move client-specific data under extensions or remove the unknown field."));
            }
        }
    }

    private static void ValidateSchema(JsonElement manifest, ICollection<ValidationFinding> findings)
    {
        if (!manifest.TryGetProperty("$schema", out var schema) ||
            schema.ValueKind != JsonValueKind.String ||
            !string.Equals(schema.GetString(), CanonicalPluginSchema, StringComparison.Ordinal))
        {
            findings.Add(ManifestRuleRegistry.Create(
                "APV-MANIFEST-002",
                "$schema is missing, not a string, or not the canonical Agent Plugins 1.0.0 manifest schema.",
                $"Set $schema to '{CanonicalPluginSchema}'."));
        }
    }

    private static void ValidateName(JsonElement manifest, ICollection<ValidationFinding> findings)
    {
        if (!manifest.TryGetProperty("name", out var name) ||
            name.ValueKind != JsonValueKind.String ||
            string.IsNullOrEmpty(name.GetString()))
        {
            findings.Add(ManifestRuleRegistry.Create(
                "APV-MANIFEST-003",
                "name is required, must be a string, and must not be empty.",
                "Provide a non-empty string name."));
            return;
        }

        var value = name.GetString()!;
        if (value.Length > 64 ||
            !char.IsAsciiLetterOrDigit(value[0]) ||
            !char.IsAsciiLetterOrDigit(value[^1]) ||
            value.Contains("--", StringComparison.Ordinal) ||
            value.Contains("..", StringComparison.Ordinal) ||
            value.Any(character => !((character is >= 'a' and <= 'z') || char.IsAsciiDigit(character) || character is '-' or '.')))
        {
            findings.Add(ManifestRuleRegistry.Create(
                "APV-MANIFEST-004",
                "name must be 1–64 lowercase alphanumeric, hyphen, or period characters, start/end alphanumeric, and contain no -- or .. sequence.",
                "Rename the plugin to a valid portable plugin name."));
        }
    }

    private static void ValidateMetadata(JsonElement manifest, ICollection<ValidationFinding> findings)
    {
        var hasInvalidMetadata = false;
        foreach (var field in new[] { "version", "description", "homepage", "repository", "license" })
        {
            hasInvalidMetadata |= manifest.TryGetProperty(field, out var value) && value.ValueKind != JsonValueKind.String;
        }

        if (manifest.TryGetProperty("keywords", out var keywords) &&
            (keywords.ValueKind != JsonValueKind.Array || keywords.EnumerateArray().Any(value => value.ValueKind != JsonValueKind.String)))
        {
            hasInvalidMetadata = true;
        }

        if (manifest.TryGetProperty("author", out var author))
        {
            hasInvalidMetadata |= author.ValueKind != JsonValueKind.Object ||
                (author.ValueKind == JsonValueKind.Object && author.EnumerateObject().Any(property =>
                    !AuthorFields.Contains(property.Name) || property.Value.ValueKind != JsonValueKind.String));
        }

        if (hasInvalidMetadata)
        {
            findings.Add(ManifestRuleRegistry.Create(
                "APV-MANIFEST-005",
                "One or more permitted metadata fields have an invalid JSON type or author shape.",
                "Use strings for scalar metadata, a string array for keywords, and only string name/email/url fields in author."));
        }
    }

    private static void ValidateExtensions(JsonElement manifest, ICollection<ValidationFinding> findings)
    {
        if (manifest.TryGetProperty("extensions", out var extensions) && extensions.ValueKind != JsonValueKind.Object)
        {
            findings.Add(ManifestRuleRegistry.Create(
                "APV-MANIFEST-007",
                "extensions is not an object and was ignored.",
                "Use an object keyed by client extension namespace, or remove extensions."));
        }
    }

    private static ManifestValidationResult Fatal(IReadOnlyList<ValidationFinding> findings) => new(
        PackageFormat.PortableAgentPlugins,
        ValidationStatus.Invalid,
        ManifestStatus.Invalid,
        ComponentDiscoveryAllowed: false,
        findings);
}
