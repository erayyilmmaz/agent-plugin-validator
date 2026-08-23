using AgentPluginValidator.Core.PackageIntake;

namespace AgentPluginValidator.Core.Tests;

public sealed class SafePackageReaderTests
{
    [Fact]
    public void Reads_a_contained_fixture_file_without_mutating_the_fixture()
    {
        var fixtureRoot = FixtureRoot("basic");
        var before = File.ReadAllText(Path.Combine(fixtureRoot, "plugin.json"));
        var creation = SafePackageReader.TryCreate(fixtureRoot);

        Assert.True(creation.IsSuccess);
        var result = creation.Reader!.ReadUtf8Text("plugin.json");

        Assert.True(result.IsSuccess);
        Assert.Equal(before, result.Value);
        Assert.Equal(before.Length, creation.Reader.TotalBytesRead);
        Assert.Equal(before, File.ReadAllText(Path.Combine(fixtureRoot, "plugin.json")));
    }

    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("nested/../../outside.txt")]
    [InlineData("/outside.txt")]
    public void Rejects_traversal_and_absolute_paths_before_reading(string requestedPath)
    {
        var creation = SafePackageReader.TryCreate(FixtureRoot("path-traversal"));

        Assert.True(creation.IsSuccess);
        var result = creation.Reader!.ReadUtf8Text(requestedPath);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Failure!.Code, new[]
        {
            PackageReadFailureCode.PathTraversal,
            PackageReadFailureCode.InvalidRelativePath
        });
        Assert.Equal(0, creation.Reader.TotalBytesRead);
    }

    [Fact]
    public void Rejects_a_symlink_that_resolves_outside_the_plugin_root()
    {
        using var temporaryRoot = TemporaryFixtureRoot.FromFixture("symlink");
        var outsideFile = Path.Combine(temporaryRoot.ParentPath, "outside.txt");
        File.WriteAllText(outsideFile, "outside-root-content");
        File.CreateSymbolicLink(Path.Combine(temporaryRoot.RootPath, "escaped.txt"), outsideFile);

        var creation = SafePackageReader.TryCreate(temporaryRoot.RootPath);
        Assert.True(creation.IsSuccess);

        var result = creation.Reader!.ReadUtf8Text("escaped.txt");

        Assert.False(result.IsSuccess);
        Assert.Equal(PackageReadFailureCode.SymlinkEscapesRoot, result.Failure!.Code);
        Assert.Equal(0, creation.Reader.TotalBytesRead);
    }

    [Fact]
    public void Rejects_an_intermediate_directory_link_that_resolves_outside_the_plugin_root()
    {
        using var temporaryRoot = TemporaryFixtureRoot.FromFixture("symlink");
        var outsideDirectory = Path.Combine(temporaryRoot.ParentPath, "outside-directory");
        Directory.CreateDirectory(outsideDirectory);
        File.WriteAllText(Path.Combine(outsideDirectory, "secret.txt"), "outside-root-content");
        Directory.CreateSymbolicLink(Path.Combine(temporaryRoot.RootPath, "escaped-directory"), outsideDirectory);

        var creation = SafePackageReader.TryCreate(temporaryRoot.RootPath);
        Assert.True(creation.IsSuccess);

        var result = creation.Reader!.ReadUtf8Text("escaped-directory/secret.txt");

        Assert.False(result.IsSuccess);
        Assert.Equal(PackageReadFailureCode.SymlinkEscapesRoot, result.Failure!.Code);
        Assert.Equal(0, creation.Reader.TotalBytesRead);
    }

    [Fact]
    public void Resolves_a_linked_plugin_root_before_reading_contained_content()
    {
        using var temporaryRoot = TemporaryFixtureRoot.FromFixture("symlink");
        var rootLink = Path.Combine(temporaryRoot.ParentPath, "plugin-root-link");
        Directory.CreateSymbolicLink(rootLink, temporaryRoot.RootPath);

        var creation = SafePackageReader.TryCreate(rootLink);
        Assert.True(creation.IsSuccess);
        Assert.Equal(Path.GetFullPath(temporaryRoot.RootPath), creation.Reader!.ResolvedRootPath);

        var result = creation.Reader.ReadUtf8Text("plugin.json");

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Reads_an_internal_symlink_after_containment_is_proven()
    {
        using var temporaryRoot = TemporaryFixtureRoot.FromFixture("symlink");
        var targetFile = Path.Combine(temporaryRoot.RootPath, "inside.txt");
        File.WriteAllText(targetFile, "inside-root-content");
        File.CreateSymbolicLink(Path.Combine(temporaryRoot.RootPath, "alias.txt"), targetFile);

        var creation = SafePackageReader.TryCreate(temporaryRoot.RootPath);
        Assert.True(creation.IsSuccess);

        var result = creation.Reader!.ReadUtf8Text("alias.txt");

        Assert.True(result.IsSuccess);
        Assert.Equal("inside-root-content", result.Value);
    }

    [Fact]
    public void Enforces_individual_and_aggregate_content_limits()
    {
        var fixtureRoot = FixtureRoot("limits");
        var individualLimitReader = SafePackageReader.TryCreate(
            fixtureRoot,
            new PackageReaderOptions(maximumFileBytes: 5, maximumTotalContentBytes: 20)).Reader!;

        var tooLarge = individualLimitReader.ReadUtf8Text("one.txt");

        Assert.False(tooLarge.IsSuccess);
        Assert.Equal(PackageReadFailureCode.FileTooLarge, tooLarge.Failure!.Code);
        Assert.Equal(0, individualLimitReader.TotalBytesRead);

        var aggregateLimitReader = SafePackageReader.TryCreate(
            fixtureRoot,
            new PackageReaderOptions(maximumFileBytes: 6, maximumTotalContentBytes: 10)).Reader!;

        Assert.True(aggregateLimitReader.ReadUtf8Text("one.txt").IsSuccess);
        var totalExceeded = aggregateLimitReader.ReadUtf8Text("two.txt");

        Assert.False(totalExceeded.IsSuccess);
        Assert.Equal(PackageReadFailureCode.TotalContentLimitExceeded, totalExceeded.Failure!.Code);
        Assert.Equal(6, aggregateLimitReader.TotalBytesRead);
    }

    [Fact]
    public void Returns_a_controlled_failure_for_missing_or_malformed_paths()
    {
        var creation = SafePackageReader.TryCreate(FixtureRoot("basic"));
        Assert.True(creation.IsSuccess);

        var missing = creation.Reader!.ReadUtf8Text("missing.json");
        var malformed = creation.Reader.ReadUtf8Text("nested//inside.txt");

        Assert.Equal(PackageReadFailureCode.PathNotFound, missing.Failure!.Code);
        Assert.Equal(PackageReadFailureCode.InvalidRelativePath, malformed.Failure!.Code);
        Assert.Equal(0, creation.Reader.TotalBytesRead);
    }

    private static string FixtureRoot(string name) =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", "apv3", name);

    private sealed class TemporaryFixtureRoot : IDisposable
    {
        private TemporaryFixtureRoot(string parentPath, string rootPath)
        {
            ParentPath = parentPath;
            RootPath = rootPath;
        }

        public string ParentPath { get; }

        public string RootPath { get; }

        public static TemporaryFixtureRoot FromFixture(string fixtureName)
        {
            var parentPath = Path.Combine(Path.GetTempPath(), "apv-tests", Guid.NewGuid().ToString("N"));
            var rootPath = Path.Combine(parentPath, "package");
            Directory.CreateDirectory(rootPath);
            CopyDirectory(FixtureRoot(fixtureName), rootPath);
            return new TemporaryFixtureRoot(parentPath, rootPath);
        }

        public void Dispose()
        {
            if (Directory.Exists(ParentPath))
            {
                Directory.Delete(ParentPath, recursive: true);
            }
        }

        private static void CopyDirectory(string source, string destination)
        {
            foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
            }

            foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                var destinationFile = Path.Combine(destination, Path.GetRelativePath(source, file));
                Directory.CreateDirectory(Path.GetDirectoryName(destinationFile)!);
                File.Copy(file, destinationFile);
            }
        }
    }
}
