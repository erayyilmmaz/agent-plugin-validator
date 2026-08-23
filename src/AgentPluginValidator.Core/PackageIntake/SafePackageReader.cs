using System.Text;

namespace AgentPluginValidator.Core.PackageIntake;

/// <summary>
/// Read-only, bounded access to files contained by one resolved plugin root.
/// It never executes, loads, or connects to plugin-provided content.
/// </summary>
public sealed class SafePackageReader
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private readonly object sync = new();
    private readonly StringComparison pathComparison;
    private readonly string rootWithSeparator;
    private long totalBytesRead;

    private SafePackageReader(string resolvedRootPath, PackageReaderOptions options)
    {
        ResolvedRootPath = resolvedRootPath;
        Options = options;
        pathComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        rootWithSeparator = EnsureTrailingDirectorySeparator(resolvedRootPath);
    }

    public string ResolvedRootPath { get; }

    public PackageReaderOptions Options { get; }

    public long TotalBytesRead
    {
        get
        {
            lock (sync)
            {
                return totalBytesRead;
            }
        }
    }

    public static PackageReaderCreationResult TryCreate(string? pluginRootPath, PackageReaderOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(pluginRootPath))
        {
            return PackageReaderCreationResult.Fail(Failure(
                PackageReadFailureCode.RootPathInvalid,
                "The plugin root path is required."));
        }

        try
        {
            var candidateRoot = Path.GetFullPath(pluginRootPath);
            if (!Directory.Exists(candidateRoot))
            {
                return PackageReaderCreationResult.Fail(Failure(
                    File.Exists(candidateRoot)
                        ? PackageReadFailureCode.RootNotDirectory
                        : PackageReadFailureCode.RootNotFound,
                    File.Exists(candidateRoot)
                        ? "The plugin root must be a directory."
                        : "The plugin root directory does not exist."));
            }

            var resolvedRoot = ResolveRootDirectory(candidateRoot);
            if (resolvedRoot is null || !Directory.Exists(resolvedRoot))
            {
                return PackageReaderCreationResult.Fail(Failure(
                    PackageReadFailureCode.RootNotDirectory,
                    "The resolved plugin root must be a directory."));
            }

            return PackageReaderCreationResult.Success(new SafePackageReader(
                Path.GetFullPath(resolvedRoot),
                options ?? new PackageReaderOptions()));
        }
        catch (Exception exception) when (IsFilesystemException(exception))
        {
            return PackageReaderCreationResult.Fail(Failure(
                PackageReadFailureCode.RootInaccessible,
                "The plugin root could not be resolved safely."));
        }
    }

    public PackageReadResult<string> ReadUtf8Text(string? relativePath)
    {
        lock (sync)
        {
            var bytesResult = ReadBytesLocked(relativePath);
            if (!bytesResult.IsSuccess)
            {
                return PackageReadResult<string>.Fail(bytesResult.Failure!);
            }

            try
            {
                return PackageReadResult<string>.Success(StrictUtf8.GetString(bytesResult.Value!));
            }
            catch (DecoderFallbackException)
            {
                return PackageReadResult<string>.Fail(Failure(
                    PackageReadFailureCode.InvalidUtf8,
                    "The contained file is not valid UTF-8 text.",
                    relativePath));
            }
        }
    }

    public PackageReadResult<string> GetContainedDirectory(string? relativePath)
    {
        lock (sync)
        {
            var segmentsResult = TrySplitRelativePath(relativePath);
            if (!segmentsResult.IsSuccess)
            {
                return PackageReadResult<string>.Fail(segmentsResult.Failure!);
            }

            var currentPath = ResolvedRootPath;
            foreach (var segment in segmentsResult.Value!)
            {
                currentPath = Path.GetFullPath(Path.Combine(currentPath, segment));
                if (!IsContained(currentPath))
                {
                    return PackageReadResult<string>.Fail(Failure(PackageReadFailureCode.PathEscapesRoot, "The requested path resolves outside the plugin root.", relativePath));
                }

                var linkResult = TryResolveLinkWithinRoot(currentPath, relativePath);
                if (!linkResult.IsSuccess)
                {
                    return PackageReadResult<string>.Fail(linkResult.Failure!);
                }

                currentPath = linkResult.Value!;
                if (!File.Exists(currentPath) && !Directory.Exists(currentPath))
                {
                    return PackageReadResult<string>.Fail(Failure(PackageReadFailureCode.PathNotFound, "The requested package path does not exist.", relativePath));
                }
            }

            return Directory.Exists(currentPath)
                ? PackageReadResult<string>.Success(currentPath)
                : PackageReadResult<string>.Fail(Failure(PackageReadFailureCode.NotRegularFile, "The requested package path is not a directory.", relativePath));
        }
    }

    private PackageReadResult<byte[]> ReadBytesLocked(string? relativePath)
    {
        var pathResult = TryResolveContainedRegularFile(relativePath);
        if (!pathResult.IsSuccess)
        {
            return PackageReadResult<byte[]>.Fail(pathResult.Failure!);
        }

        try
        {
            using var stream = new FileStream(
                pathResult.Value!,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 16 * 1024,
                FileOptions.SequentialScan);

            var length = stream.Length;
            if (length > Options.MaximumFileBytes)
            {
                return PackageReadResult<byte[]>.Fail(Failure(
                    PackageReadFailureCode.FileTooLarge,
                    "The contained file exceeds the configured per-file read limit.",
                    relativePath));
            }

            if (length > Options.MaximumTotalContentBytes - totalBytesRead)
            {
                return PackageReadResult<byte[]>.Fail(Failure(
                    PackageReadFailureCode.TotalContentLimitExceeded,
                    "Reading this file would exceed the configured aggregate content limit.",
                    relativePath));
            }

            var bytes = new byte[checked((int)length)];
            var offset = 0;
            while (offset < bytes.Length)
            {
                var read = stream.Read(bytes, offset, bytes.Length - offset);
                if (read == 0)
                {
                    totalBytesRead += offset;
                    return PackageReadResult<byte[]>.Fail(Failure(
                        PackageReadFailureCode.IoFailure,
                        "The contained file changed while it was being read.",
                        relativePath));
                }

                offset += read;
            }

            totalBytesRead += bytes.Length;
            return PackageReadResult<byte[]>.Success(bytes);
        }
        catch (Exception exception) when (IsFilesystemException(exception))
        {
            return PackageReadResult<byte[]>.Fail(Failure(
                PackageReadFailureCode.IoFailure,
                "The contained file could not be read safely.",
                relativePath));
        }
    }

    private PackageReadResult<string> TryResolveContainedRegularFile(string? relativePath)
    {
        var segmentsResult = TrySplitRelativePath(relativePath);
        if (!segmentsResult.IsSuccess)
        {
            return PackageReadResult<string>.Fail(segmentsResult.Failure!);
        }

        var currentPath = ResolvedRootPath;
        foreach (var segment in segmentsResult.Value!)
        {
            currentPath = Path.GetFullPath(Path.Combine(currentPath, segment));
            if (!IsContained(currentPath))
            {
                return PackageReadResult<string>.Fail(Failure(
                    PackageReadFailureCode.PathEscapesRoot,
                    "The requested path resolves outside the plugin root.",
                    relativePath));
            }

            var linkResult = TryResolveLinkWithinRoot(currentPath, relativePath);
            if (!linkResult.IsSuccess)
            {
                return PackageReadResult<string>.Fail(linkResult.Failure!);
            }

            currentPath = linkResult.Value!;
            if (!File.Exists(currentPath) && !Directory.Exists(currentPath))
            {
                return PackageReadResult<string>.Fail(Failure(
                    PackageReadFailureCode.PathNotFound,
                    "The requested package path does not exist.",
                    relativePath));
            }
        }

        if (Directory.Exists(currentPath))
        {
            return PackageReadResult<string>.Fail(Failure(
                PackageReadFailureCode.NotRegularFile,
                "The requested package path is not a regular file.",
                relativePath));
        }

        if (!File.Exists(currentPath))
        {
            return PackageReadResult<string>.Fail(Failure(
                PackageReadFailureCode.PathNotFound,
                "The requested package path does not exist.",
                relativePath));
        }

        return PackageReadResult<string>.Success(currentPath);
    }

    private PackageReadResult<string[]> TrySplitRelativePath(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || relativePath.IndexOf('\0') >= 0)
        {
            return PackageReadResult<string[]>.Fail(Failure(
                PackageReadFailureCode.InvalidRelativePath,
                "A non-empty relative package path is required.",
                relativePath));
        }

        var normalizedPath = relativePath.Replace('\\', '/');
        if (Path.IsPathRooted(relativePath) || normalizedPath.StartsWith("/", StringComparison.Ordinal))
        {
            return PackageReadResult<string[]>.Fail(Failure(
                PackageReadFailureCode.InvalidRelativePath,
                "An absolute package path is not allowed.",
                relativePath));
        }

        var segments = normalizedPath.Split('/', StringSplitOptions.None);
        foreach (var segment in segments)
        {
            if (segment == "..")
            {
                return PackageReadResult<string[]>.Fail(Failure(
                    PackageReadFailureCode.PathTraversal,
                    "Parent-directory traversal is not allowed.",
                    relativePath));
            }

            if (string.IsNullOrEmpty(segment) || segment == "." || ContainsInvalidFileNameCharacter(segment))
            {
                return PackageReadResult<string[]>.Fail(Failure(
                    PackageReadFailureCode.InvalidRelativePath,
                    "The package path contains a malformed path segment.",
                    relativePath));
            }
        }

        return PackageReadResult<string[]>.Success(segments);
    }

    private PackageReadResult<string> TryResolveLinkWithinRoot(string candidatePath, string? relativePath)
    {
        try
        {
            var directoryInfo = new DirectoryInfo(candidatePath);
            var fileInfo = new FileInfo(candidatePath);
            var isDirectory = Directory.Exists(candidatePath);
            var linkTarget = isDirectory ? directoryInfo.LinkTarget : fileInfo.LinkTarget;

            if (linkTarget is null)
            {
                return PackageReadResult<string>.Success(candidatePath);
            }

            FileSystemInfo linkInfo = isDirectory ? directoryInfo : fileInfo;
            var target = linkInfo.ResolveLinkTarget(returnFinalTarget: true);
            if (target is null)
            {
                return PackageReadResult<string>.Fail(Failure(
                    PackageReadFailureCode.LinkUnresolvable,
                    "A package link could not be resolved safely.",
                    relativePath));
            }

            var resolvedTarget = Path.GetFullPath(target.FullName);
            if (!IsContained(resolvedTarget))
            {
                return PackageReadResult<string>.Fail(Failure(
                    PackageReadFailureCode.SymlinkEscapesRoot,
                    "A package link resolves outside the plugin root.",
                    relativePath));
            }

            return PackageReadResult<string>.Success(resolvedTarget);
        }
        catch (Exception exception) when (IsFilesystemException(exception))
        {
            return PackageReadResult<string>.Fail(Failure(
                PackageReadFailureCode.LinkUnresolvable,
                "A package link could not be resolved safely.",
                relativePath));
        }
    }

    private static string? ResolveRootDirectory(string candidateRoot)
    {
        var directoryInfo = new DirectoryInfo(candidateRoot);
        if (directoryInfo.LinkTarget is null)
        {
            return candidateRoot;
        }

        var target = directoryInfo.ResolveLinkTarget(returnFinalTarget: true);
        return target is DirectoryInfo ? target.FullName : null;
    }

    private bool IsContained(string candidatePath) =>
        candidatePath.Equals(ResolvedRootPath, pathComparison) ||
        candidatePath.StartsWith(rootWithSeparator, pathComparison);

    private static bool ContainsInvalidFileNameCharacter(string segment) =>
        segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0;

    private static string EnsureTrailingDirectorySeparator(string path) =>
        path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;

    private static bool IsFilesystemException(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException;

    private static PackageReadFailure Failure(
        PackageReadFailureCode code,
        string message,
        string? relativePath = null) => new(code, message, relativePath);
}
