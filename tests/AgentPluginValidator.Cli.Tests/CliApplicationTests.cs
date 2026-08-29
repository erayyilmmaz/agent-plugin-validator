public sealed class CliApplicationTests
{
    [Fact]
    public void Validates_a_package_through_the_public_command_and_renders_a_human_report()
    {
        var result = Run("validate", Fixture("apv6", "absent"));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Overall: VALID", result.Output);
        Assert.Contains("Skills: ABSENT", result.Output);
        Assert.Contains("MCP: ABSENT", result.Output);
        Assert.Contains("Errors: 0", result.Output);
        Assert.DoesNotContain("%", result.Output);
    }

    [Fact]
    public void Returns_invalid_and_keeps_component_local_detail_in_the_human_report()
    {
        var result = Run("validate", Fixture("apv6", "mixed"));

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Overall: INVALID", result.Output);
        Assert.Contains("MCP: PARTIAL", result.Output);
        Assert.Contains("APV-MCP-020", result.Output);
    }

    [Theory]
    [InlineData("codex-only")]
    [InlineData("copilot-root")]
    [InlineData("claude-only")]
    [InlineData("legacy-openplugin-only")]
    public void Returns_not_applicable_for_a_recognized_vendor_only_package(string fixture)
    {
        var result = Run("validate", Fixture("apv4", fixture));

        Assert.Equal(3, result.ExitCode);
        Assert.Contains("Overall: NOT_APPLICABLE", result.Output);
        Assert.Contains("APV-FORMAT-001", result.Output);
    }

    [Fact]
    public void Returns_input_error_for_usage_or_directory_problems()
    {
        var usage = Run("validate");
        var missingDirectory = Run("validate", Path.Combine(Path.GetTempPath(), "apv-does-not-exist"));

        Assert.Equal(2, usage.ExitCode);
        Assert.Contains("Usage:", usage.Error);
        Assert.Equal(2, missingDirectory.ExitCode);
        Assert.Contains("Input error:", missingDirectory.Error);
    }

    [Fact]
    public void Supports_quiet_and_ci_output_modes_without_changing_exit_status()
    {
        var quiet = Run("validate", Fixture("apv6", "absent"), "--quiet");
        var ci = Run("validate", Fixture("apv6", "absent"), "--ci");

        Assert.Equal(0, quiet.ExitCode);
        Assert.Equal(string.Empty, quiet.Output);
        Assert.Equal(0, ci.ExitCode);
        Assert.Equal("STATUS=VALID ERRORS=0 WARNINGS=0 INFO=0 MANIFEST=VALID SKILLS=ABSENT MCP=ABSENT" + Environment.NewLine, ci.Output);
    }

    private static CliResult Run(params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        return new CliResult(CliApplication.Run(args, output, error), output.ToString(), error.ToString());
    }

    private static string Fixture(string task, string fixture) => Path.Combine(AppContext.BaseDirectory, "fixtures", task, fixture);

    private sealed record CliResult(int ExitCode, string Output, string Error);
}
