using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Domain.SharedKernel.Interfaces;
using Cratebase.Domain.SharedKernel.Errors;
using Cratebase.Domain.SharedKernel.Validation;

namespace Cratebase.Domain.Imports;

public sealed class ReleaseImportDraft : IEntity<ReleaseImportDraftId>
{
    private string _artistNamesJson = "[]";
    private string _artistCreditsJson = "[]";
    private string _genresJson = "[]";
    private string _issuesJson = "[]";
    private string _labelsJson = "[]";
    private string _selectedArtistIdsJson = "[]";
    private string _tagsJson = "[]";

    private ReleaseImportDraft()
    {
        SourcePath = string.Empty;
        RelativePath = string.Empty;
        Title = string.Empty;
        Type = "unknown";
    }

    private ReleaseImportDraft(CollectionId collectionId, ReleaseImportSessionId sessionId, ReleaseImportDraftId id, string sourcePath, string relativePath)
        : this()
    {
        CollectionId = collectionId;
        SessionId = sessionId;
        Id = id;
        SourcePath = Guard.RequiredText(sourcePath, nameof(sourcePath), "release_import.source_path_required");
        RelativePath = relativePath;
        Status = ReleaseImportDraftStatus.NeedsReview;
    }

    public CollectionId CollectionId { get; private set; }
    public ReleaseImportSessionId SessionId { get; private set; }
    public ReleaseImportDraftId Id { get; private set; }
    public string SourcePath { get; private set; }
    public string RelativePath { get; private set; }
    public ReleaseImportDraftStatus Status { get; private set; }
    public string Title { get; private set; }
    public string Type { get; private set; }
    public string? CatalogNumber { get; private set; }
    public string? LabelName { get; private set; }
    public DateOnly? ReleaseDate { get; private set; }
    public int? Year { get; private set; }
    public bool IsVariousArtists { get; private set; }
    public bool NotOnLabel { get; private set; }
    public string? CoverPath { get; private set; }
    public string? CoverFileName { get; private set; }
    public string? CoverExtension { get; private set; }
    public string? CoverContentType { get; private set; }
    public long? CoverSizeBytes { get; private set; }
    public byte[]? CoverContent { get; private set; }
    public ReleaseId? ConfirmedReleaseId { get; private set; }
    public IReadOnlyList<string> ArtistNames => ImportJson.Deserialize<string>(_artistNamesJson);
    public IReadOnlyList<ReleaseImportArtistCredit> ArtistCredits => ImportJson.Deserialize<ReleaseImportArtistCredit>(_artistCreditsJson);
    public IReadOnlyList<ReleaseImportLabel> Labels => ImportJson.Deserialize<ReleaseImportLabel>(_labelsJson);
    public IReadOnlyList<Guid> SelectedArtistIds => ImportJson.Deserialize<Guid>(_selectedArtistIdsJson);
    public IReadOnlyList<string> Genres => ImportJson.Deserialize<string>(_genresJson);
    public IReadOnlyList<string> Tags => ImportJson.Deserialize<string>(_tagsJson);
    public IReadOnlyList<ImportReviewIssue> Issues => ImportJson.Deserialize<ImportReviewIssue>(_issuesJson);

    public static ReleaseImportDraft Create(CollectionId collectionId, ReleaseImportSessionId sessionId, ReleaseImportDraftId id, string sourcePath, string relativePath)
    {
        return new ReleaseImportDraft(collectionId, sessionId, id, sourcePath, relativePath);
    }

    public void UpdateEditableFields(ReleaseImportDraftEditableFields fields)
    {
        EnsureEditable();

        Title = Guard.RequiredText(fields.Title, nameof(fields.Title), "release_import.title_required");
        Type = string.IsNullOrWhiteSpace(fields.Type) ? "unknown" : fields.Type.Trim();
        CatalogNumber = TrimOrNull(fields.CatalogNumber);
        LabelName = TrimOrNull(fields.LabelName);
        ReleaseDate = fields.ReleaseDate;
        Year = fields.Year ?? fields.ReleaseDate?.Year;
        IsVariousArtists = fields.IsVariousArtists;
        NotOnLabel = fields.NotOnLabel;
        CoverPath = TrimOrNull(fields.CoverPath);
        _artistNamesJson = ImportJson.Serialize(fields.ArtistNames);
        _artistCreditsJson = ImportJson.Serialize(NormalizeArtistCredits(fields.ArtistCredits, fields.ArtistNames, fields.SelectedArtistIds));
        _labelsJson = ImportJson.Serialize(NormalizeLabels(fields.Labels, fields.LabelName, fields.CatalogNumber));
        _selectedArtistIdsJson = ImportJson.Serialize(fields.SelectedArtistIds);
        _genresJson = ImportJson.Serialize(fields.Genres);
        _tagsJson = ImportJson.Serialize(fields.Tags);
        _issuesJson = ImportJson.Serialize(fields.Issues);
        Status = fields.Issues.Any(issue => issue.Severity == "error")
            ? ReleaseImportDraftStatus.NeedsReview
            : ReleaseImportDraftStatus.Ready;
    }

    public void SetCoverArtifact(ReleaseImportCoverArtifact? artifact)
    {
        CoverFileName = TrimOrNull(artifact?.FileName);
        CoverExtension = TrimOrNull(artifact?.Extension);
        CoverContentType = TrimOrNull(artifact?.ContentType);
        CoverSizeBytes = artifact?.SizeBytes;
        CoverContent = artifact?.Content;
    }

    public void Confirm(ReleaseId releaseId)
    {
        ConfirmedReleaseId = releaseId;
        Status = ReleaseImportDraftStatus.Confirmed;
    }

    public void Skip()
    {
        if (Status == ReleaseImportDraftStatus.Confirmed)
        {
            throw new DomainException("release_import_draft.confirmed", "Confirmed release import drafts cannot be skipped");
        }

        Status = ReleaseImportDraftStatus.Skipped;
    }

    private void EnsureEditable()
    {
        if (Status == ReleaseImportDraftStatus.Confirmed)
        {
            throw new DomainException("release_import_draft.confirmed", "Confirmed release import drafts cannot be edited");
        }

        if (Status == ReleaseImportDraftStatus.Skipped)
        {
            throw new DomainException("release_import_draft.skipped", "Skipped release import drafts cannot be edited");
        }
    }

    private static string? TrimOrNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static List<ReleaseImportArtistCredit> NormalizeArtistCredits(
        IReadOnlyList<ReleaseImportArtistCredit>? artistCredits,
        IReadOnlyList<string> artistNames,
        IReadOnlyList<Guid> selectedArtistIds)
    {
        if (artistCredits is { Count: > 0 })
        {
            return
            [
                .. artistCredits
                    .Select(credit => new ReleaseImportArtistCredit(
                        credit.ArtistId,
                        TrimOrNull(credit.Name) ?? string.Empty,
                        TrimOrNull(credit.Role) ?? string.Empty))
                    .Where(credit => credit.ArtistId is not null || !string.IsNullOrWhiteSpace(credit.Name))
            ];
        }

        List<ReleaseImportArtistCredit> credits = [];
        for (int index = 0; index < artistNames.Count; index++)
        {
            string? name = TrimOrNull(artistNames[index]);
            Guid? artistId = index < selectedArtistIds.Count ? selectedArtistIds[index] : null;
            if (artistId is null && string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            credits.Add(new ReleaseImportArtistCredit(artistId, name ?? string.Empty, "mainArtist"));
        }

        return credits;
    }

    private static List<ReleaseImportLabel> NormalizeLabels(
        IReadOnlyList<ReleaseImportLabel>? labels,
        string? legacyLabelName,
        string? legacyCatalogNumber)
    {
        if (labels is { Count: > 0 })
        {
            return
            [
                .. labels
                    .Select(label => new ReleaseImportLabel(
                        label.LabelId,
                        TrimOrNull(label.Name) ?? string.Empty,
                        TrimOrNull(label.CatalogNumber),
                        label.HasNoCatalogNumber))
                    .Where(label => label.LabelId is not null || !string.IsNullOrWhiteSpace(label.Name))
            ];
        }

        string? labelName = TrimOrNull(legacyLabelName);
        return labelName is null
            ? []
            : [new ReleaseImportLabel(null, labelName, TrimOrNull(legacyCatalogNumber), string.IsNullOrWhiteSpace(legacyCatalogNumber))];
    }
}

public sealed record ReleaseImportArtistCredit(Guid? ArtistId, string Name, string Role);

public sealed record ReleaseImportLabel(Guid? LabelId, string Name, string? CatalogNumber, bool HasNoCatalogNumber);

public sealed record ReleaseImportDraftEditableFields(
    string Title,
    string Type,
    string? CatalogNumber,
    string? LabelName,
    DateOnly? ReleaseDate,
    int? Year,
    bool IsVariousArtists,
    bool NotOnLabel,
    string? CoverPath,
    IReadOnlyList<string> ArtistNames,
    IReadOnlyList<ReleaseImportArtistCredit> ArtistCredits,
    IReadOnlyList<ReleaseImportLabel> Labels,
    IReadOnlyList<Guid> SelectedArtistIds,
    IReadOnlyList<string> Genres,
    IReadOnlyList<string> Tags,
    IReadOnlyList<ImportReviewIssue> Issues);
