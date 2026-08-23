using System.Net;
using System.Text.Json;
using AgentPluginValidator.Core.PackageIntake;

namespace AgentPluginValidator.Core.Validation;

public enum McpComponentStatus { Valid, Invalid, Partial, Absent, NotEvaluated }

public sealed record McpValidationResult(
    McpComponentStatus Status,
    int DiscoveredCount,
    int ValidCount,
    int InvalidCount,
    IReadOnlyList<ValidationFinding> Findings);

/// <summary>
/// Static-only validation for the portable root mcp.json configuration. It does
/// not resolve executables, expand variables, connect to endpoints, or expose
/// configured values to a process.
/// </summary>
public sealed class McpValidator
{
    public const string CanonicalMcpSchema = "https://agent-plugins.org/schemas/1.0.0/mcp.schema.json";

    private static readonly HashSet<string> TopLevelFields = new(StringComparer.Ordinal) { "$schema", "mcpServers" };
    private static readonly HashSet<string> StdioFields = new(StringComparer.Ordinal) { "type", "command", "args", "env", "cwd" };
    private static readonly HashSet<string> RemoteFields = new(StringComparer.Ordinal) { "type", "url", "headers" };
    private static readonly HashSet<string> DeterministicSecretNames = new(StringComparer.Ordinal)
    {
        "API_KEY", "API_TOKEN", "ACCESS_TOKEN", "AUTH_TOKEN", "CLIENT_SECRET", "PASSWORD", "SECRET", "TOKEN"
    };
    private static readonly HashSet<string> DeterministicSecretHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Authorization", "Proxy-Authorization", "Cookie", "Set-Cookie", "X-Api-Key", "X-Auth-Token"
    };

    public McpValidationResult Validate(SafePackageReader reader, ManifestValidationResult manifest)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(manifest);
        if (!manifest.ComponentDiscoveryAllowed)
        {
            return new McpValidationResult(McpComponentStatus.NotEvaluated, 0, 0, 0, Array.Empty<ValidationFinding>());
        }

        var read = reader.ReadUtf8Text("mcp.json");
        if (!read.IsSuccess)
        {
            return read.Failure!.Code == PackageReadFailureCode.PathNotFound
                ? new McpValidationResult(McpComponentStatus.Absent, 0, 0, 0, Array.Empty<ValidationFinding>())
                : InvalidComponent(read.Failure!.Code == PackageReadFailureCode.NotRegularFile ? "APV-COMPONENT-002" : "APV-MCP-001",
                    "mcp.json could not be read as a contained regular configuration file.",
                    "Provide a contained root mcp.json regular file, or remove the optional component.");
        }

        try
        {
            using var document = JsonDocument.Parse(read.Value!);
            return ValidateDocument(reader, manifest, document.RootElement);
        }
        catch (JsonException)
        {
            return InvalidComponent("APV-MCP-001", "mcp.json is not valid JSON.", "Fix mcp.json so it is a JSON object with $schema and mcpServers.");
        }
    }

    private static McpValidationResult ValidateDocument(SafePackageReader reader, ManifestValidationResult manifest, JsonElement root)
    {
        var findings = new List<ValidationFinding>();
        if (root.ValueKind != JsonValueKind.Object)
        {
            findings.Add(ComponentFinding("APV-MCP-001", "mcp.json must contain a top-level JSON object.", "Replace mcp.json with a JSON object."));
            return InvalidComponent(findings);
        }

        var properties = root.EnumerateObject().ToArray();
        var schemaProperties = properties.Where(property => property.NameEquals("$schema")).ToArray();
        var serversProperties = properties.Where(property => property.NameEquals("mcpServers")).ToArray();
        if (properties.Any(property => !TopLevelFields.Contains(property.Name)) || schemaProperties.Length != 1 || serversProperties.Length != 1 || serversProperties.FirstOrDefault().Value.ValueKind != JsonValueKind.Object)
        {
            findings.Add(ComponentFinding("APV-MCP-001", "mcp.json must contain exactly the $schema and mcpServers fields, and mcpServers must be an object.", "Use only the required top-level fields and make mcpServers an object."));
        }

        var schemaValue = schemaProperties.Length == 1 && schemaProperties[0].Value.ValueKind == JsonValueKind.String
            ? schemaProperties[0].Value.GetString()
            : null;
        if (!string.Equals(schemaValue, CanonicalMcpSchema, StringComparison.Ordinal))
        {
            findings.Add(ComponentFinding("APV-MCP-002", "$schema is missing, not a string, or not the canonical Agent Plugins 1.0.0 MCP schema.", $"Set $schema to '{CanonicalMcpSchema}'."));
        }

        if (schemaValue is not null && !string.Equals(VersionFromSchema(schemaValue), VersionFromSchema(PortableManifestValidator.CanonicalPluginSchema), StringComparison.Ordinal))
        {
            findings.Add(ComponentFinding("APV-CROSS-001", "mcp.json declares an Agent Plugins schema version different from plugin.json.", "Use an MCP schema version that exactly matches plugin.json."));
        }

        if (findings.Count != 0)
        {
            return InvalidComponent(findings);
        }

        var valid = 0;
        foreach (var server in serversProperties[0].Value.EnumerateObject())
        {
            if (ValidateServer(reader, server.Name, server.Value, findings)) valid++;
        }

        var discovered = serversProperties[0].Value.GetArrayLengthOrObjectPropertyCount();
        var invalid = discovered - valid;
        var status = invalid == 0 ? McpComponentStatus.Valid : valid == 0 ? McpComponentStatus.Invalid : McpComponentStatus.Partial;
        return new McpValidationResult(status, discovered, valid, invalid, findings);
    }

    private static bool ValidateServer(SafePackageReader reader, string serverName, JsonElement server, ICollection<ValidationFinding> findings)
    {
        var initialCount = findings.Count;
        if (server.ValueKind != JsonValueKind.Object || !server.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String)
        {
            findings.Add(EntryFinding("APV-MCP-003", serverName, "Every MCP server must be an object with a supported type.", "Use exactly one stdio, streamable-http, or sse server variant."));
            return false;
        }

        var typeName = type.GetString();
        var allowedFields = typeName == "stdio" ? StdioFields : typeName is "streamable-http" or "sse" ? RemoteFields : null;
        if (allowedFields is null || server.EnumerateObject().Any(property => !allowedFields.Contains(property.Name)))
        {
            findings.Add(EntryFinding("APV-MCP-003", serverName, "The MCP server type is unsupported or contains fields outside its closed transport variant.", "Use only the fields defined for one supported transport variant."));
            return false;
        }

        if (typeName == "stdio") ValidateStdio(reader, serverName, server, findings);
        else ValidateRemote(serverName, server, findings);
        return findings.Count == initialCount;
    }

    private static void ValidateStdio(SafePackageReader reader, string serverName, JsonElement server, ICollection<ValidationFinding> findings)
    {
        if (!server.TryGetProperty("command", out var command) || command.ValueKind != JsonValueKind.String || !IsCommand(command.GetString()!, reader))
        {
            findings.Add(EntryFinding("APV-MCP-010", serverName, "stdio.command must be one bare executable token or a contained ./ plugin-relative path without placeholder expansion.", "Use one bare executable name or a contained ./ path, and pass arguments separately."));
        }

        if (server.TryGetProperty("args", out var args) && (args.ValueKind != JsonValueKind.Array || args.EnumerateArray().Any(value => value.ValueKind != JsonValueKind.String)))
        {
            findings.Add(EntryFinding("APV-MCP-012", serverName, "stdio.args must be an array of strings.", "Use string arguments; only ${PLUGIN_ROOT} and ${PLUGIN_DATA} are expanded by clients."));
        }

        if (server.TryGetProperty("cwd", out var cwd) && (cwd.ValueKind != JsonValueKind.String || !IsCwd(cwd.GetString()!, reader)))
        {
            findings.Add(EntryFinding("APV-MCP-011", serverName, "stdio.cwd must be a contained ./ path or an allowed ${PLUGIN_ROOT}/${PLUGIN_DATA} form.", "Use ./..., ${PLUGIN_ROOT}[/...], or ${PLUGIN_DATA}[/...] without traversal."));
        }

        if (!server.TryGetProperty("env", out var environment)) return;
        if (environment.ValueKind != JsonValueKind.Object || environment.EnumerateObject().Any(property => property.Value.ValueKind != JsonValueKind.String || !IsEnvironmentName(property.Name)))
        {
            findings.Add(EntryFinding("APV-MCP-012", serverName, "stdio.env must be an object with valid environment names and string values.", "Use string values and ordinary environment variable names; placeholders are allowed only in values."));
            return;
        }

        if (environment.EnumerateObject().Any(property => property.Name is "PLUGIN_ROOT" or "PLUGIN_DATA"))
        {
            findings.Add(EntryFinding("APV-MCP-013", serverName, "stdio.env must not configure the reserved PLUGIN_ROOT or PLUGIN_DATA variables.", "Remove reserved environment entries; the client supplies them."));
        }

        if (environment.EnumerateObject().Any(property => HasDeterministicSecretName(property.Name) || HasDeterministicSecretValue(property.Value.GetString()!)))
        {
            findings.Add(EntryFinding("APV-MCP-023", serverName, "stdio.env contains a deterministically identifiable secret-bearing entry.", "Remove credentials from portable env configuration and use client-managed authorization."));
        }
    }

    private static void ValidateRemote(string serverName, JsonElement server, ICollection<ValidationFinding> findings)
    {
        if (!server.TryGetProperty("url", out var url) || url.ValueKind != JsonValueKind.String || !IsAllowedRemoteUrl(url.GetString()!))
        {
            findings.Add(EntryFinding("APV-MCP-020", serverName, "Remote MCP URL must be absolute HTTP(S), without user info or fragment; non-loopback hosts require HTTPS.", "Use HTTPS, or HTTP only for exact localhost or a loopback IP, without credentials or fragments."));
        }

        if (!server.TryGetProperty("headers", out var headers)) return;
        if (headers.ValueKind != JsonValueKind.Object || !AreValidHeaders(headers))
        {
            findings.Add(EntryFinding("APV-MCP-021", serverName, "Remote headers must be unique case-insensitive HTTP fields with string values and no recognized placeholders.", "Use unique valid header names and literal values without ${PLUGIN_ROOT} or ${PLUGIN_DATA}."));
            return;
        }

        if (headers.EnumerateObject().Any(property => DeterministicSecretHeaders.Contains(property.Name) || HasDeterministicSecretValue(property.Value.GetString()!)))
        {
            findings.Add(EntryFinding("APV-MCP-022", serverName, "Remote headers contain a deterministically identifiable credential or secret.", "Remove credentials from portable headers and use client-managed authorization."));
        }
    }

    private static bool IsCommand(string value, SafePackageReader reader)
    {
        if (string.IsNullOrEmpty(value) || value.IndexOfAny([' ', '\t', '\r', '\n', '\0']) >= 0 || HasRecognizedPlaceholder(value)) return false;
        if (value.StartsWith("./", StringComparison.Ordinal))
        {
            return value.Length > 2 && reader.ValidateContainedPluginRelativePath(value[2..]).IsSuccess;
        }

        return value.IndexOfAny(['/', '\\']) < 0 && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.');
    }

    private static bool IsCwd(string value, SafePackageReader reader)
    {
        if (value == "./") return true;
        if (value.StartsWith("./", StringComparison.Ordinal)) return reader.ValidateContainedPluginRelativePath(value[2..]).IsSuccess;
        if (value == "${PLUGIN_ROOT}" || value == "${PLUGIN_DATA}") return true;
        if (value.StartsWith("${PLUGIN_ROOT}/", StringComparison.Ordinal)) return reader.ValidateContainedPluginRelativePath(value[15..]).IsSuccess;
        if (value.StartsWith("${PLUGIN_DATA}/", StringComparison.Ordinal)) return IsSafeDataRelativePath(value[15..]);
        return false;
    }

    private static bool IsSafeDataRelativePath(string value) => value.Length > 0 && value.Split('/').All(segment => segment.Length > 0 && segment is not "." and not ".." && segment.IndexOf('\0') < 0);

    private static bool IsAllowedRemoteUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") || uri.UserInfo.Length != 0 || uri.Fragment.Length != 0) return false;
        var loopback = string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase) || (IPAddress.TryParse(uri.Host, out var address) && IPAddress.IsLoopback(address));
        return uri.Scheme == "https" || loopback;
    }

    private static bool AreValidHeaders(JsonElement headers)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in headers.EnumerateObject())
        {
            if (!seen.Add(header.Name) || header.Value.ValueKind != JsonValueKind.String || !IsHttpToken(header.Name) || !IsHeaderValue(header.Value.GetString()!) || HasRecognizedPlaceholder(header.Name) || HasRecognizedPlaceholder(header.Value.GetString()!)) return false;
        }
        return true;
    }

    private static bool IsEnvironmentName(string value) => value.Length > 0 && (char.IsAsciiLetter(value[0]) || value[0] == '_') && value.All(character => char.IsAsciiLetterOrDigit(character) || character == '_');
    private static bool IsHttpToken(string value) => value.Length > 0 && value.All(character => char.IsAsciiLetterOrDigit(character) || "!#$%&'*+-.^_`|~".Contains(character));
    private static bool IsHeaderValue(string value) => value.All(character => character is '\t' || (character >= ' ' && character <= '~'));
    private static bool HasRecognizedPlaceholder(string value) => value.Contains("${PLUGIN_ROOT}", StringComparison.Ordinal) || value.Contains("${PLUGIN_DATA}", StringComparison.Ordinal);
    private static bool HasDeterministicSecretName(string value) => DeterministicSecretNames.Contains(value);
    private static bool HasDeterministicSecretValue(string value) => value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) || value.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase) || value.StartsWith("sk-", StringComparison.Ordinal);
    private static string? VersionFromSchema(string value)
    {
        const string prefix = "https://agent-plugins.org/schemas/";
        if (!value.StartsWith(prefix, StringComparison.Ordinal)) return null;
        var remainder = value[prefix.Length..];
        var separator = remainder.IndexOf('/');
        return separator > 0 ? remainder[..separator] : null;
    }

    private static ValidationFinding ComponentFinding(string ruleId, string explanation, string fix) => ManifestRuleRegistry.Create(ruleId, explanation, fix);
    private static ValidationFinding EntryFinding(string ruleId, string serverName, string explanation, string fix) => ManifestRuleRegistry.Create(ruleId, explanation, fix) with { FilePath = $"mcp.json#mcpServers.{serverName}" };
    private static McpValidationResult InvalidComponent(string ruleId, string explanation, string fix) => InvalidComponent(new[] { ComponentFinding(ruleId, explanation, fix) });
    private static McpValidationResult InvalidComponent(IReadOnlyList<ValidationFinding> findings) => new(McpComponentStatus.Invalid, 0, 0, 0, findings);
}

internal static class JsonElementMcpExtensions
{
    public static int GetArrayLengthOrObjectPropertyCount(this JsonElement value) => value.ValueKind == JsonValueKind.Object ? value.EnumerateObject().Count() : value.GetArrayLength();
}
