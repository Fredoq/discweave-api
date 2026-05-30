using DiscWeave.Domain.Collection;
using DiscWeave.Domain.SharedKernel.Errors;
using DiscWeave.Domain.SharedKernel.Ids;
using DiscWeave.Domain.SharedKernel.Interfaces;
using DiscWeave.Domain.SharedKernel.Optional;
using DiscWeave.Domain.SharedKernel.Validation;

namespace DiscWeave.Domain.Imports;

public sealed class ReleaseImportDraftTrack : IEntity<ReleaseImportDraftTrackId>
{
    private string _artistCreditsJson = "[]";
    private string _artistNamesJson = "[]";
    private string? _contentHash;
    private string _issuesJson = "[]";
    private string _selectedArtistIdsJson = "[]";

    private ReleaseImportDraftTrack()
    {
        FilePath = string.Empty;
        RelativePath = string.Empty;
        Title = string.Empty;
    }

    private ReleaseImportDraftTrack(CollectionId collectionId, ReleaseImportDraftId draftId, ReleaseImportDraftTrackId id, DraftTrackFileInfo file)
        : this()
    {
        CollectionId = collectionId;
        DraftId = draftId;
        Id = id;
        FilePath = Guard.RequiredText(file.FilePath, nameof(file.FilePath), "release_import.track_file_required");
        RelativePath = file.RelativePath;
        Format = file.Format;
        SizeBytes = file.SizeBytes;
        LastModifiedAt = file.LastModifiedAt;
        SetContentHash(file.ContentHash);
    }

    public CollectionId CollectionId { get; private set; }
    public ReleaseImportDraftId DraftId { get; private set; }
    public ReleaseImportDraftTrackId Id { get; private set; }
    public string FilePath { get; private set; }
    public string RelativePath { get; private set; }
    public AudioFileFormat Format { get; private set; }
    public long SizeBytes { get; private set; }
    public DateTimeOffset LastModifiedAt { get; private set; }
    public IOptionalValue<string> ContentHash => _contentHash is null ? Optional.Missing<string>() : Optional.From(_contentHash);
    public TimeSpan? Duration { get; private set; }
    public int? Position { get; private set; }
    public string Title { get; private set; }
    public bool IsSkipped { get; private set; }
    public TrackId? SelectedTrackId { get; private set; }
    public IReadOnlyList<ReleaseImportArtistCredit> ArtistCredits => ImportJson.Deserialize<ReleaseImportArtistCredit>(_artistCreditsJson);
    public IReadOnlyList<string> ArtistNames => ImportJson.Deserialize<string>(_artistNamesJson);
    public IReadOnlyList<Guid> SelectedArtistIds => ImportJson.Deserialize<Guid>(_selectedArtistIdsJson);
    public IReadOnlyList<ImportReviewIssue> Issues => ImportJson.Deserialize<ImportReviewIssue>(_issuesJson);

    public static ReleaseImportDraftTrack Create(CollectionId collectionId, ReleaseImportDraftId draftId, ReleaseImportDraftTrackId id, DraftTrackFileInfo file)
    {
        return new ReleaseImportDraftTrack(collectionId, draftId, id, file);
    }

    public void UpdateEditableFields(DraftTrackEditableFields fields)
    {
        if (fields.Position is < 1)
        {
            throw new DomainException("release_import.track_position_invalid", "Release import track position must be greater than zero");
        }

        Position = fields.Position;
        Title = Guard.RequiredText(fields.Title, nameof(fields.Title), "release_import.track_title_required");
        Duration = fields.Duration;
        IsSkipped = fields.IsSkipped;
        SelectedTrackId = fields.SelectedTrackId;
        _artistCreditsJson = ImportJson.Serialize(NormalizeArtistCredits(fields.ArtistCredits, fields.ArtistNames, fields.SelectedArtistIds));
        _artistNamesJson = ImportJson.Serialize(fields.ArtistNames);
        _selectedArtistIdsJson = ImportJson.Serialize(fields.SelectedArtistIds);
        _issuesJson = ImportJson.Serialize(fields.Issues);
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

    private void SetContentHash(IOptionalValue<string> contentHash)
    {
        _contentHash = contentHash is PresentOptionalValue<string> presentContentHash
            ? TrimOrNull(presentContentHash.Value)?.ToLowerInvariant()
            : null;
    }

    private static string? TrimOrNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
