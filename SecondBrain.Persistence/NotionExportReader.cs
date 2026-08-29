using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using SecondBrain.Application.NotionAudit;

namespace SecondBrain.Persistence;

public sealed class NotionExportReader : INotionExportReader
{
    private const string UnresolvedRelationTarget = "unresolved-export-relation";

    private static readonly Regex NotionIdPattern = new(
        @"(?<![0-9a-f])(?:[0-9a-f]{32}|[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})(?![0-9a-f])",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public async Task<NotionExportMetadata> ReadAsync(
        string sourcePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        if (Directory.Exists(sourcePath))
        {
            return await ReadDirectoryAsync(sourcePath, cancellationToken);
        }

        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("The selected Notion export is no longer available.");
        }

        var extension = Path.GetExtension(sourcePath);
        if (extension.Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            return await ReadArchiveAsync(sourcePath, cancellationToken);
        }

        await using var stream = File.OpenRead(sourcePath);
        return extension.Equals(".json", StringComparison.OrdinalIgnoreCase)
            ? await ReadManifestAsync(stream, cancellationToken)
            : extension.Equals(".csv", StringComparison.OrdinalIgnoreCase)
                ? new NotionExportMetadata(
                    [await ReadCsvAsync(Path.GetFileName(sourcePath), stream, cancellationToken)],
                    CsvDiagnostics([ParseDatabaseIdentity(Path.GetFileName(sourcePath))]))
                : throw new NotSupportedException(
                    "Choose a Notion export folder, .zip archive, synthetic manifest, or CSV file.");
    }

    private static async Task<NotionExportMetadata> ReadDirectoryAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var manifests = Directory.EnumerateFiles(path, "*.json", SearchOption.AllDirectories)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        foreach (var manifest in manifests)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var stream = File.OpenRead(manifest);
            try
            {
                return await ReadManifestAsync(stream, cancellationToken);
            }
            catch (JsonException)
            {
                // Continue to CSV metadata when an unrelated JSON export page is present.
            }
        }

        var tables = new List<NotionExportTableMetadata>();
        foreach (var file in Directory.EnumerateFiles(path, "*.csv", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var stream = File.OpenRead(file);
            tables.Add(await ReadCsvAsync(
                Path.GetRelativePath(path, file),
                stream,
                cancellationToken));
        }

        if (tables.Count == 0)
        {
            throw new InvalidDataException("No supported Notion manifest or CSV tables were found.");
        }

        return new NotionExportMetadata(tables, CsvDiagnostics(tables));
    }

    private static async Task<NotionExportMetadata> ReadArchiveAsync(
        string path,
        CancellationToken cancellationToken)
    {
        using var archive = ZipFile.OpenRead(path);
        foreach (var entry in archive.Entries
            .Where(entry => entry.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            .OrderBy(entry => entry.FullName, StringComparer.OrdinalIgnoreCase))
        {
            await using var stream = entry.Open();
            try
            {
                return await ReadManifestAsync(stream, cancellationToken);
            }
            catch (JsonException)
            {
                // Ignore unrelated JSON pages in the archive.
            }
        }

        var tables = new List<NotionExportTableMetadata>();
        foreach (var entry in archive.Entries.Where(entry =>
            entry.FullName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var stream = entry.Open();
            tables.Add(await ReadCsvAsync(
                entry.FullName,
                stream,
                cancellationToken));
        }

        if (tables.Count == 0)
        {
            throw new InvalidDataException("The archive contains no supported Notion manifest or CSV tables.");
        }

        return new NotionExportMetadata(tables, CsvDiagnostics(tables));
    }

    private static async Task<NotionExportMetadata> ReadManifestAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;
        if (!root.TryGetProperty("files", out var files) || files.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("The JSON file is not a supported Notion export manifest.");
        }

        var tables = new List<NotionExportTableMetadata>();
        foreach (var file in files.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fileName = OptionalString(file, "fileName") ?? "export.csv";
            var fields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var rows = new List<NotionExportRowMetadata>();
            if (file.TryGetProperty("rows", out var rowElements) &&
                rowElements.ValueKind == JsonValueKind.Array)
            {
                foreach (var row in rowElements.EnumerateArray())
                {
                    var relations = new List<NotionExportRelation>();
                    foreach (var property in row.EnumerateObject())
                    {
                        fields.Add(property.Name);
                        if (IsRelationshipField(property.Name))
                        {
                            relations.Add(new NotionExportRelation(
                                property.Name,
                                RelationshipTargets(property.Value)));
                        }
                    }

                    rows.Add(new NotionExportRowMetadata(
                        NormalizeOptionalNotionId(OptionalString(row, "notionId")),
                        OptionalString(row, "classification"),
                        OptionalBoolean(row, "isTemplate"),
                        OptionalBoolean(row, "archived"),
                        relations));
                }
            }

            tables.Add(new NotionExportTableMetadata(
                fileName,
                OptionalString(file, "database"),
                NormalizeOptionalNotionId(OptionalString(file, "databaseNotionId")),
                Path.GetFileNameWithoutExtension(fileName)
                    .EndsWith("_all", StringComparison.OrdinalIgnoreCase),
                fields.ToArray(),
                rows));
        }

        return new NotionExportMetadata(tables, []);
    }

    private static async Task<NotionExportTableMetadata> ReadCsvAsync(
        string sourceName,
        Stream stream,
        CancellationToken cancellationToken)
    {
        var isDuplicateAllView = IsDuplicateAllView(sourceName);
        var identity = ParseDatabaseIdentity(sourceName);
        using var reader = new StreamReader(stream, Encoding.UTF8, true, leaveOpen: true);
        var text = await reader.ReadToEndAsync(cancellationToken);
        var records = ParseCsv(text);
        if (records.Count == 0)
        {
            return new NotionExportTableMetadata(sourceName, identity.DatabaseName, identity.DatabaseNotionId,
                isDuplicateAllView,
                [], []);
        }

        var headers = records[0];
        var rows = records.Skip(1)
            .Where(record => record.Any(value => !string.IsNullOrWhiteSpace(value)))
            .Select(record =>
            {
                var values = headers.Select((header, index) =>
                    new KeyValuePair<string, string>(header, index < record.Count ? record[index] : string.Empty))
                    .ToDictionary(StringComparer.OrdinalIgnoreCase);
                var relations = values
                    .Where(value => IsRelationshipField(value.Key))
                    .Select(value => CsvRelationship(value.Key, value.Value))
                    .ToArray();
                values.TryGetValue("Notion ID", out var notionId);
                values.TryGetValue("Classification", out var classification);
                return new NotionExportRowMetadata(
                    NormalizeOptionalNotionId(notionId),
                    classification,
                    values.TryGetValue("Is template", out var template) && bool.TryParse(template, out var isTemplate) && isTemplate,
                    values.TryGetValue("Archived", out var archived) && bool.TryParse(archived, out var isArchived) && isArchived,
                    relations);
            })
            .ToArray();
        return new NotionExportTableMetadata(
            sourceName,
            identity.DatabaseName,
            identity.DatabaseNotionId,
            isDuplicateAllView,
            headers,
            rows);
    }

    private static bool IsRelationshipField(string fieldName)
    {
        var normalized = fieldName.Replace(" ", string.Empty, StringComparison.Ordinal);
        return normalized.Contains("relation", StringComparison.OrdinalIgnoreCase) ||
            (!normalized.Equals("notionid", StringComparison.OrdinalIgnoreCase) &&
             (normalized.EndsWith("notionid", StringComparison.OrdinalIgnoreCase) ||
              normalized.EndsWith("notionids", StringComparison.OrdinalIgnoreCase))) ||
            normalized.Equals("tags", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("links", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("placement", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("primaryplacement", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("project", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("projects", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("area", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("areas", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("resource", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("resources", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("resourcetopic", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("resourcetopics", StringComparison.OrdinalIgnoreCase);
    }

    private static NotionExportRelation CsvRelationship(string fieldName, string value)
    {
        var references = value.Split(
            [',', ';'],
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var targets = references
            .SelectMany(reference => NotionIdPattern.Matches(reference).Select(match => NormalizeNotionId(match.Value)))
            .Concat(references
                .Where(reference => !NotionIdPattern.IsMatch(reference))
                .Select(_ => UnresolvedRelationTarget))
            .ToArray();
        return new NotionExportRelation(fieldName, targets);
    }

    private static IReadOnlyList<string> RelationshipTargets(JsonElement value)
    {
        var references = value.ValueKind switch
        {
            JsonValueKind.String => value.GetString()!
                .Split([',', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
            JsonValueKind.Array => value.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString()!)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToArray(),
            _ => [],
        };
        return references
            .SelectMany(reference => NotionIdPattern.Matches(reference)
                .Select(match => NormalizeNotionId(match.Value)))
            .Concat(references
                .Where(reference => !NotionIdPattern.IsMatch(reference))
                .Select(_ => UnresolvedRelationTarget))
            .ToArray();
    }

    private static bool IsDuplicateAllView(string sourceName) =>
        HasDuplicateViewSuffix(RemoveTrailingNotionId(Path.GetFileNameWithoutExtension(sourceName)));

    private static bool HasDuplicateViewSuffix(string value) =>
        value.EndsWith("_all", StringComparison.OrdinalIgnoreCase);

    private static NotionExportTableMetadata ParseDatabaseIdentity(string sourceName)
    {
        var stem = Uri.UnescapeDataString(Path.GetFileNameWithoutExtension(sourceName)).Trim();
        var match = NotionIdPattern.Matches(stem)
            .LastOrDefault(candidate =>
            {
                var suffix = stem[(candidate.Index + candidate.Length)..].Trim();
                return suffix.Length == 0 || HasDuplicateViewSuffix(suffix);
            });
        var databaseId = match is not null ? NormalizeNotionId(match.Value) : null;
        var name = match is not null ? stem.Remove(match.Index, match.Length).Trim() : stem;
        name = name.TrimEnd();
        if (HasDuplicateViewSuffix(name))
        {
            name = name[..^4].TrimEnd();
        }

        return new NotionExportTableMetadata(sourceName,
            string.IsNullOrWhiteSpace(name) ? null : name,
            databaseId,
            IsDuplicateAllView(sourceName),
            [], []);
    }

    private static string RemoveTrailingNotionId(string value)
    {
        var match = NotionIdPattern.Match(value);
        return match.Success && string.IsNullOrWhiteSpace(value[(match.Index + match.Length)..])
            ? value[..match.Index].TrimEnd()
            : value;
    }

    private static string NormalizeNotionId(string value) => value.Replace("-", string.Empty, StringComparison.Ordinal);

    private static string? NormalizeOptionalNotionId(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : NormalizeNotionId(value);

    private static IReadOnlyList<string> CsvDiagnostics(IEnumerable<NotionExportTableMetadata> tables)
    {
        var unidentified = tables.Count(table => string.IsNullOrWhiteSpace(table.DatabaseNotionId));
        return unidentified == 0
            ? []
            : [$"{unidentified} CSV table(s) do not contain a Notion database ID in the export filename and remain ambiguous."];
    }

    private static List<List<string>> ParseCsv(string text)
    {
        var records = new List<List<string>>();
        var record = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (character == '"')
            {
                if (quoted && index + 1 < text.Length && text[index + 1] == '"')
                {
                    field.Append('"');
                    index++;
                }
                else
                {
                    quoted = !quoted;
                }
            }
            else if (character == ',' && !quoted)
            {
                record.Add(field.ToString());
                field.Clear();
            }
            else if ((character == '\r' || character == '\n') && !quoted)
            {
                if (character == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
                {
                    index++;
                }

                record.Add(field.ToString());
                field.Clear();
                records.Add(record);
                record = [];
            }
            else
            {
                field.Append(character);
            }
        }

        if (quoted)
        {
            throw new InvalidDataException("The selected export contains malformed CSV with an unterminated quoted field.");
        }

        if (field.Length > 0 || record.Count > 0)
        {
            record.Add(field.ToString());
            records.Add(record);
        }

        return records;
    }

    private static string? OptionalString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) &&
        property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static bool OptionalBoolean(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) &&
        property.ValueKind is JsonValueKind.True or JsonValueKind.False &&
        property.GetBoolean();
}
