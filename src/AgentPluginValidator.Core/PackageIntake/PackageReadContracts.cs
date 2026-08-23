namespace AgentPluginValidator.Core.PackageIntake;

public enum PackageReadFailureCode
{
    RootPathInvalid,
    RootNotFound,
    RootNotDirectory,
    RootInaccessible,
    InvalidRelativePath,
    PathTraversal,
    PathEscapesRoot,
    SymlinkEscapesRoot,
    LinkUnresolvable,
    PathNotFound,
    NotRegularFile,
    FileTooLarge,
    TotalContentLimitExceeded,
    InvalidUtf8,
    IoFailure
}

public sealed record PackageReadFailure(
    PackageReadFailureCode Code,
    string Message,
    string? RelativePath = null);

public sealed class PackageReadResult<T>
{
    private PackageReadResult(T? value, PackageReadFailure? failure)
    {
        Value = value;
        Failure = failure;
    }

    public T? Value { get; }

    public PackageReadFailure? Failure { get; }

    public bool IsSuccess => Failure is null;

    public static PackageReadResult<T> Success(T value) => new(value, null);

    public static PackageReadResult<T> Fail(PackageReadFailure failure) => new(default, failure);
}

public sealed class PackageReaderCreationResult
{
    private PackageReaderCreationResult(SafePackageReader? reader, PackageReadFailure? failure)
    {
        Reader = reader;
        Failure = failure;
    }

    public SafePackageReader? Reader { get; }

    public PackageReadFailure? Failure { get; }

    public bool IsSuccess => Reader is not null;

    internal static PackageReaderCreationResult Success(SafePackageReader reader) => new(reader, null);

    internal static PackageReaderCreationResult Fail(PackageReadFailure failure) => new(null, failure);
}

public sealed record PackageReaderOptions
{
    public const long DefaultMaximumFileBytes = 1 * 1024 * 1024;
    public const long DefaultMaximumTotalContentBytes = 10 * 1024 * 1024;

    public PackageReaderOptions(
        long maximumFileBytes = DefaultMaximumFileBytes,
        long maximumTotalContentBytes = DefaultMaximumTotalContentBytes)
    {
        if (maximumFileBytes <= 0 || maximumFileBytes > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumFileBytes),
                "Maximum file bytes must be between 1 and Int32.MaxValue.");
        }

        if (maximumTotalContentBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumTotalContentBytes),
                "Maximum total content bytes must be greater than zero.");
        }

        MaximumFileBytes = maximumFileBytes;
        MaximumTotalContentBytes = maximumTotalContentBytes;
    }

    public long MaximumFileBytes { get; }

    public long MaximumTotalContentBytes { get; }
}
