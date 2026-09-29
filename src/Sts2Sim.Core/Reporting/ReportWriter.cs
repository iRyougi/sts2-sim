using System.Text;
using System.Text.Json;

namespace Sts2Sim.Core.Reporting;

public static class ReportWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
    };

    private static readonly Encoding Utf8WithoutByteOrderMark = new UTF8Encoding(false);
    internal delegate Task WriteFileAsync(
        string path,
        string contents,
        CancellationToken cancellationToken);

    /// <summary>
    /// Publishes one immutable report directory. <paramref name="outputDirectory"/> must not
    /// already exist; after a successful return it contains the complete validated report.
    /// </summary>
    /// <remarks>
    /// The report is written and re-read from a unique sibling staging directory, then published
    /// with one same-filesystem <see cref="Directory.Move(string, string)"/>. Cancellation is
    /// observed during staging and immediately before that move. The move itself is synchronous
    /// and non-cancelable, so cancellation racing after the final check may still publish the
    /// complete report successfully.
    /// </remarks>
    /// <exception cref="IOException">
    /// The output path already exists or staging/publication encounters an I/O failure.
    /// </exception>
    public static Task WriteAsync(
        string outputDirectory,
        RunManifest manifest,
        IReadOnlyDictionary<string, CombatLog> combatLogs,
        CancellationToken cancellationToken = default) =>
        WriteAsyncCore(
            outputDirectory,
            manifest,
            combatLogs,
            (string?)null,
            PhysicalWriteAllTextAsync,
            cancellationToken);

    /// <summary>Publishes JSON plus the fixed <c>report.md</c> artifact in one immutable directory.</summary>
    public static Task WriteWithMarkdownAsync(
        string outputDirectory,
        RunManifest manifest,
        IReadOnlyDictionary<string, CombatLog> combatLogs,
        string markdown,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        return WriteAsyncCore(
            outputDirectory,
            manifest,
            combatLogs,
            markdown,
            PhysicalWriteAllTextAsync,
            cancellationToken);
    }

    internal static Task WriteWithFileWriterAsync(
        string outputDirectory,
        RunManifest manifest,
        IReadOnlyDictionary<string, CombatLog> combatLogs,
        WriteFileAsync writeFileAsync,
        CancellationToken cancellationToken = default) =>
        WriteAsyncCore(
            outputDirectory,
            manifest,
            combatLogs,
            (string?)null,
            writeFileAsync,
            cancellationToken);

    internal static Task WriteWithMarkdownAndFileWriterAsync(
        string outputDirectory,
        RunManifest manifest,
        IReadOnlyDictionary<string, CombatLog> combatLogs,
        string markdown,
        WriteFileAsync writeFileAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        return WriteAsyncCore(
            outputDirectory, manifest, combatLogs, markdown, writeFileAsync, cancellationToken);
    }

    private static async Task WriteAsyncCore(
        string outputDirectory,
        RunManifest manifest,
        IReadOnlyDictionary<string, CombatLog> combatLogs,
        string? markdown,
        WriteFileAsync writeFileAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(combatLogs);
        ArgumentNullException.ThrowIfNull(writeFileAsync);
        cancellationToken.ThrowIfCancellationRequested();

        string rootDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(outputDirectory));
        if (Directory.Exists(rootDirectory) || File.Exists(rootDirectory))
        {
            throw new IOException($"Report output path '{rootDirectory}' already exists.");
        }

        DirectoryInfo? parentDirectory = Directory.GetParent(rootDirectory);
        string reportDirectoryName = Path.GetFileName(rootDirectory);
        if (parentDirectory is null || string.IsNullOrWhiteSpace(reportDirectoryName))
        {
            throw new ArgumentException("Output directory must not be a filesystem root.", nameof(outputDirectory));
        }

        IReadOnlyList<(string RelativePath, string Json)> files = BuildSerializedFiles(
            manifest,
            combatLogs);
        Directory.CreateDirectory(parentDirectory.FullName);
        string stagingDirectory = Path.Combine(
            parentDirectory.FullName,
            $".{reportDirectoryName}.staging-{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(stagingDirectory);
            Directory.CreateDirectory(Path.Combine(stagingDirectory, "combats"));
            foreach ((string relativePath, string json) in files)
            {
                await writeFileAsync(
                        Path.Combine(stagingDirectory, relativePath),
                        json,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            if (markdown is not null)
            {
                await writeFileAsync(
                        Path.Combine(stagingDirectory, "report.md"),
                        markdown,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            await ValidateStagedReportAsync(
                    stagingDirectory,
                    manifest,
                    combatLogs,
                    cancellationToken)
                .ConfigureAwait(false);
            if (markdown is not null)
            {
                byte[] actualMarkdown = await File.ReadAllBytesAsync(
                        Path.Combine(stagingDirectory, "report.md"),
                        cancellationToken)
                    .ConfigureAwait(false);
                byte[] expectedMarkdown = Utf8WithoutByteOrderMark.GetBytes(markdown);
                if (!actualMarkdown.AsSpan().SequenceEqual(expectedMarkdown))
                {
                    throw new IOException("Staged report.md does not match the requested UTF-8 content.");
                }
            }

            cancellationToken.ThrowIfCancellationRequested();

            Directory.Move(stagingDirectory, rootDirectory);
        }
        catch
        {
            try
            {
                DeleteGeneratedStagingDirectory(
                    stagingDirectory,
                    parentDirectory.FullName,
                    reportDirectoryName);
            }
            catch
            {
                // Best-effort cleanup: never mask the original failure with a cleanup failure.
            }

            throw;
        }
    }

    private static IReadOnlyList<(string RelativePath, string Json)> BuildSerializedFiles(
        RunManifest manifest,
        IReadOnlyDictionary<string, CombatLog> combatLogs)
    {
        ReportSetValidator.Validate(manifest, combatLogs);
        var files = new List<(string RelativePath, string Json)>(combatLogs.Count + 1)
        {
            ("run_manifest.json", Serialize(manifest)),
        };

        foreach (CombatLog log in combatLogs.Values
                     .OrderBy(log => log.Floor)
                     .ThenBy(log => log.CombatId, StringComparer.Ordinal))
        {
            files.Add((
                Path.Combine("combats", $"{log.Floor:D3}_{log.CombatId}.json"),
                Serialize(log)));
        }

        return files;
    }

    private static async Task ValidateStagedReportAsync(
        string stagingDirectory,
        RunManifest originalManifest,
        IReadOnlyDictionary<string, CombatLog> originalLogs,
        CancellationToken cancellationToken)
    {
        string manifestJson = await File.ReadAllTextAsync(
                Path.Combine(stagingDirectory, "run_manifest.json"),
                cancellationToken)
            .ConfigureAwait(false);
        RunManifest stagedManifest = JsonSerializer.Deserialize<RunManifest>(
            manifestJson,
            SerializerOptions) ?? throw new JsonException("Staged run manifest deserialized to null.");

        string combatsDirectory = Path.Combine(stagingDirectory, "combats");
        string[] actualFiles = Directory.GetFiles(combatsDirectory, "*.json", SearchOption.TopDirectoryOnly);
        var stagedLogs = new Dictionary<string, CombatLog>(StringComparer.Ordinal);
        foreach (string file in actualFiles)
        {
            string json = await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false);
            CombatLog log = JsonSerializer.Deserialize<CombatLog>(json, SerializerOptions) ??
                throw new JsonException($"Staged combat log '{Path.GetFileName(file)}' deserialized to null.");
            if (!stagedLogs.TryAdd(log.CombatId, log))
            {
                throw new JsonException($"Staged combat logs contain duplicate ID '{log.CombatId}'.");
            }
        }

        ReportSetValidator.Validate(stagedManifest, stagedLogs);

        string[] expectedFileNames = stagedManifest.CombatLogs
            .Select(reference => Path.GetFileName(reference.CombatLogFile))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        string[] actualFileNames = actualFiles
            .Select(Path.GetFileName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray()!;
        if (!expectedFileNames.SequenceEqual(actualFileNames, StringComparer.Ordinal) ||
            !string.Equals(stagedManifest.RunId, originalManifest.RunId, StringComparison.Ordinal) ||
            !string.Equals(stagedManifest.SchemaVersion, originalManifest.SchemaVersion, StringComparison.Ordinal) ||
            !stagedManifest.CombatLogs.SequenceEqual(originalManifest.CombatLogs) ||
            stagedLogs.Count != originalLogs.Count)
        {
            throw new JsonException("Staged report identity or file set does not match the requested report.");
        }

        foreach ((string combatId, CombatLog original) in originalLogs)
        {
            if (!stagedLogs.TryGetValue(combatId, out CombatLog? staged) ||
                staged.Floor != original.Floor ||
                !string.Equals(staged.RunId, original.RunId, StringComparison.Ordinal) ||
                !string.Equals(staged.SchemaVersion, original.SchemaVersion, StringComparison.Ordinal))
            {
                throw new JsonException($"Staged combat log '{combatId}' has different critical identity fields.");
            }
        }
    }

    private static string Serialize<T>(T value) =>
        JsonSerializer.Serialize(value, SerializerOptions);

    private static Task PhysicalWriteAllTextAsync(
        string path,
        string contents,
        CancellationToken cancellationToken) =>
        File.WriteAllTextAsync(path, contents, Utf8WithoutByteOrderMark, cancellationToken);

    private static void DeleteGeneratedStagingDirectory(
        string stagingDirectory,
        string expectedParent,
        string reportDirectoryName)
    {
        if (!Directory.Exists(stagingDirectory))
        {
            return;
        }

        string fullPath = Path.GetFullPath(stagingDirectory);
        string? parent = Directory.GetParent(fullPath)?.FullName;
        string name = Path.GetFileName(fullPath);
        bool isExpectedStagingDirectory =
            string.Equals(parent, Path.GetFullPath(expectedParent), StringComparison.OrdinalIgnoreCase) &&
            name.StartsWith($".{reportDirectoryName}.staging-", StringComparison.Ordinal);
        if (!isExpectedStagingDirectory)
        {
            throw new IOException("Refusing to clean an unexpected staging directory path.");
        }

        Directory.Delete(fullPath, recursive: true);
    }
}
