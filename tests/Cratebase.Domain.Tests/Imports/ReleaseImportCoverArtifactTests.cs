using Cratebase.Domain.Imports;

namespace Cratebase.Domain.Tests.Imports;

public sealed class ReleaseImportCoverArtifactTests
{
    [Fact(DisplayName = "Release import cover artifact equality includes metadata and content")]
    public void Release_import_cover_artifact_equality_includes_metadata_and_content()
    {
        var first = new ReleaseImportCoverArtifact("cover.jpg", ".jpg", "image/jpeg", 3, [1, 2, 3]);
        var same = new ReleaseImportCoverArtifact("cover.jpg", ".jpg", "image/jpeg", 3, [1, 2, 3]);
        var differentName = new ReleaseImportCoverArtifact("front.jpg", ".jpg", "image/jpeg", 3, [1, 2, 3]);
        var differentContent = new ReleaseImportCoverArtifact("cover.jpg", ".jpg", "image/jpeg", 3, [1, 2, 4]);

        Assert.Equal(first, same);
        Assert.True(first.Equals((object)same));
        Assert.Equal(first.GetHashCode(), same.GetHashCode());
        Assert.NotEqual(first, differentName);
        Assert.NotEqual(first, differentContent);
        Assert.False(first.Equals(null));
        Assert.False(first.Equals("cover.jpg"));
    }

    [Fact(DisplayName = "Release import cover artifact guards invalid content")]
    public void Release_import_cover_artifact_guards_invalid_content()
    {
        _ = Assert.Throws<ArgumentNullException>(() => new ReleaseImportCoverArtifact(
            "cover.jpg",
            ".jpg",
            "image/jpeg",
            0,
            null!));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new ReleaseImportCoverArtifact(
            "cover.jpg",
            ".jpg",
            "image/jpeg",
            -1,
            []));
        _ = Assert.Throws<ArgumentException>(() => new ReleaseImportCoverArtifact(
            "cover.jpg",
            ".jpg",
            "image/jpeg",
            2,
            [1]));
    }
}
