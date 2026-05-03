using Cratebase.Domain.Catalog;
using Cratebase.Domain.Credits;
using Cratebase.Domain.SharedKernel.Errors;
using Cratebase.Domain.SharedKernel.Ids;

namespace Cratebase.Domain.Tests.Credits;

public sealed class CreditTests
{
    [Fact]
    public void Credit_links_an_artist_contributor_to_a_release_or_track_target_through_a_role()
    {
        var collectionId = CollectionId.New();
        var person = Person.Create(collectionId, ArtistId.New(), "Arthur Baker");
        var group = Group.Create(collectionId, ArtistId.New(), "New Order");
        var releaseId = ReleaseId.New();
        var trackId = TrackId.New();
        var releaseCredit = Credit.Create(collectionId,
            CreditId.New(),
            CreditContributor.FromArtist(group),
            CreditTarget.ForRelease(releaseId),
            CreditRole.MainArtist);
        var trackCredit = Credit.Create(collectionId,
            CreditId.New(),
            CreditContributor.FromArtist(person),
            CreditTarget.ForTrack(trackId),
            CreditRole.Producer);

        Assert.True(releaseCredit.Target.IsRelease);
        Assert.Equal(releaseId, Assert.IsType<ReleaseCreditTarget>(releaseCredit.Target).ReleaseId);
        Assert.Equal(group.Id, releaseCredit.Contributor.ArtistId);
        Assert.True(trackCredit.Target.IsTrack);
        Assert.Equal(trackId, Assert.IsType<TrackCreditTarget>(trackCredit.Target).TrackId);
        Assert.Equal(person.Id, trackCredit.Contributor.ArtistId);
        Assert.Equal(CreditRole.Producer, trackCredit.Role);
    }

    [Fact]
    public void Credit_targets_use_distinct_types_for_release_and_track_references()
    {
        var releaseTarget = CreditTarget.ForRelease(ReleaseId.New());
        var trackTarget = CreditTarget.ForTrack(TrackId.New());

        _ = Assert.IsType<ReleaseCreditTarget>(releaseTarget);
        _ = Assert.IsType<TrackCreditTarget>(trackTarget);
    }

    [Fact]
    public void Credit_roles_are_a_closed_object_catalog()
    {
        Assert.Equal(CreditRole.Producer, CreditRole.Producer);
        Assert.NotEqual(CreditRole.Producer, CreditRole.Composer);
    }

    [Fact]
    public void Credit_rejects_undefined_roles()
    {
        DomainException exception = Assert.Throws<DomainException>(() =>
            Credit.Create(CollectionId.New(),
                CreditId.New(),
                CreditContributor.FromArtist(Person.Create(CollectionId.New(), ArtistId.New(), "Arthur Baker")),
                CreditTarget.ForRelease(ReleaseId.New()),
                default));

        Assert.Equal("credit.role_invalid", exception.Code);
    }
}
