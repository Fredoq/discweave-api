using Cratebase.Domain.Catalog;
using Cratebase.Domain.SharedKernel.Errors;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Domain.SharedKernel.Optional;

namespace Cratebase.Domain.Tests.Catalog;

public sealed class ReleaseEntryValueTests
{
    [Fact]
    public void Release_track_normalizes_optional_text_values()
    {
        var trimmedTrack = ReleaseTrack.Create(
            TrackId.New(),
            TrackPosition.FromNumber(1),
            Optional.From("  Extended mix  "),
            Optional.From("  Promo edit  "));
        var blankTrack = ReleaseTrack.Create(
            TrackId.New(),
            TrackPosition.FromNumber(2),
            Optional.From("   "),
            Optional.From("   "));

        Assert.Equal("Extended mix", Assert.IsType<PresentOptionalValue<string>>(trimmedTrack.TitleOverride).Value);
        Assert.Equal("Promo edit", Assert.IsType<PresentOptionalValue<string>>(trimmedTrack.VersionNote).Value);
        Assert.False(blankTrack.TitleOverride.HasValue);
        Assert.False(blankTrack.VersionNote.HasValue);
    }

    [Fact]
    public void Release_label_normalizes_catalog_number_and_rejects_blank_values()
    {
        var releaseLabel = ReleaseLabel.Create(LabelId.New(), Optional.From("  FAC 73  "), false);

        DomainException exception = Assert.Throws<DomainException>(() =>
            ReleaseLabel.Create(LabelId.New(), Optional.From("   "), false));

        Assert.Equal("FAC 73", Assert.IsType<PresentOptionalValue<string>>(releaseLabel.CatalogNumber).Value);
        Assert.Equal("release_label.catalog_number_required", exception.Code);
    }
}
