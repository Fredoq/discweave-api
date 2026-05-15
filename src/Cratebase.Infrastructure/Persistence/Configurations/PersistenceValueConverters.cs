using Cratebase.Domain.Catalog;
using Cratebase.Domain.Ratings;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Domain.SharedKernel.Optional;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Text.Json;

namespace Cratebase.Infrastructure.Persistence.Configurations;

internal static class PersistenceValueConverters
{
    public static readonly ValueConverter<ArtistId, Guid> ArtistId = new(
        id => id.Value,
        value => new ArtistId(value));

    public static readonly ValueConverter<ArtistId?, Guid?> NullableArtistId = new(
        id => id.HasValue ? id.Value.Value : null,
        value => value.HasValue ? new ArtistId(value.Value) : null);

    public static readonly ValueConverter<CollectionId, Guid> CollectionId = new(
        id => id.Value,
        value => new CollectionId(value));

    public static readonly ValueConverter<UserId, Guid> UserId = new(
        id => id.Value,
        value => new UserId(value));

    public static readonly ValueConverter<LabelId, Guid> LabelId = new(
        id => id.Value,
        value => new LabelId(value));

    public static readonly ValueConverter<LabelId?, Guid?> NullableLabelId = new(
        id => id.HasValue ? id.Value.Value : null,
        value => value.HasValue ? new LabelId(value.Value) : null);

    public static readonly ValueConverter<ReleaseId, Guid> ReleaseId = new(
        id => id.Value,
        value => new ReleaseId(value));

    public static readonly ValueConverter<ReleaseId?, Guid?> NullableReleaseId = new(
        id => id.HasValue ? id.Value.Value : null,
        value => value.HasValue ? new ReleaseId(value.Value) : null);

    public static readonly ValueConverter<TrackId, Guid> TrackId = new(
        id => id.Value,
        value => new TrackId(value));

    public static readonly ValueConverter<TrackId?, Guid?> NullableTrackId = new(
        id => id.HasValue ? id.Value.Value : null,
        value => value.HasValue ? new TrackId(value.Value) : null);

    public static readonly ValueConverter<OwnedItemId, Guid> OwnedItemId = new(
        id => id.Value,
        value => new OwnedItemId(value));

    public static readonly ValueConverter<CreditId, Guid> CreditId = new(
        id => id.Value,
        value => new CreditId(value));

    public static readonly ValueConverter<RatingCriterionId, Guid> RatingCriterionId = new(
        id => id.Value,
        value => new RatingCriterionId(value));

    public static readonly ValueConverter<RatingValueId, Guid> RatingValueId = new(
        id => id.Value,
        value => new RatingValueId(value));

    public static readonly ValueConverter<CollectionDictionaryEntryId, Guid> CollectionDictionaryEntryId = new(
        id => id.Value,
        value => new CollectionDictionaryEntryId(value));

    public static readonly ValueConverter<ArtistRelationId, Guid> ArtistRelationId = new(
        id => id.Value,
        value => new ArtistRelationId(value));

    public static readonly ValueConverter<TrackRelationId, Guid> TrackRelationId = new(
        id => id.Value,
        value => new TrackRelationId(value));

    public static readonly ValueConverter<IOptionalValue<CoverImage>, string?> OptionalCoverImage = new(
        value => OptionalStringValue(value, CoverImageToMetadataJson),
        value => OptionalCoverImageValue(value));

    public static readonly ValueComparer<IOptionalValue<CoverImage>> OptionalCoverImageComparer = OptionalComparer<IOptionalValue<CoverImage>, string?>(
        value => OptionalStringValue(value, CoverImageToMetadataJson),
        OptionalCoverImageValue);

    public static readonly ValueConverter<IOptionalValue<DateOnly>, DateOnly?> OptionalDateOnly = new(
        value => OptionalStructValue(value, releaseDate => releaseDate),
        value => value.HasValue ? Optional.From(value.Value) : Optional.Missing<DateOnly>());

    public static readonly ValueComparer<IOptionalValue<DateOnly>> OptionalDateOnlyComparer = OptionalComparer<IOptionalValue<DateOnly>, DateOnly?>(
        value => OptionalStructValue(value, releaseDate => releaseDate),
        value => value.HasValue ? Optional.From(value.Value) : Optional.Missing<DateOnly>());

    public static readonly ValueConverter<IOptionalValue<int>, int?> OptionalInt = new(
        value => OptionalStructValue(value, number => number),
        value => value.HasValue ? Optional.From(value.Value) : Optional.Missing<int>());

    public static readonly ValueComparer<IOptionalValue<int>> OptionalIntComparer = OptionalComparer<IOptionalValue<int>, int?>(
        value => OptionalStructValue(value, number => number),
        value => value.HasValue ? Optional.From(value.Value) : Optional.Missing<int>());

    public static readonly ValueConverter<IOptionalValue<LabelId>, Guid?> OptionalLabelId = new(
        value => OptionalStructValue(value, id => id.Value),
        value => value.HasValue ? Optional.From(new LabelId(value.Value)) : Optional.Missing<LabelId>());

    public static readonly ValueComparer<IOptionalValue<LabelId>> OptionalLabelIdComparer = OptionalComparer<IOptionalValue<LabelId>, Guid?>(
        value => OptionalStructValue(value, id => id.Value),
        value => value.HasValue ? Optional.From(new LabelId(value.Value)) : Optional.Missing<LabelId>());

    public static readonly ValueConverter<IOptionalValue<Rating>, int?> OptionalRating = new(
        value => OptionalStructValue(value, rating => rating.Value),
        value => value.HasValue ? Optional.From(Rating.FromValue(value.Value)) : Optional.Missing<Rating>());

    public static readonly ValueConverter<Rating, int> RatingValue = new(
        value => value.Value,
        value => Rating.FromValue(value));

    public static readonly ValueComparer<IOptionalValue<Rating>> OptionalRatingComparer = OptionalComparer<IOptionalValue<Rating>, int?>(
        value => OptionalStructValue(value, rating => rating.Value),
        value => value.HasValue ? Optional.From(Rating.FromValue(value.Value)) : Optional.Missing<Rating>());

    public static readonly ValueConverter<IOptionalValue<string>, string?> OptionalString = new(
        value => OptionalStringValue(value, text => text),
        value => OptionalStringValue(value));

    public static readonly ValueComparer<IOptionalValue<string>> OptionalStringComparer = OptionalComparer<IOptionalValue<string>, string?>(
        value => OptionalStringValue(value, text => text),
        OptionalStringValue);

    public static readonly ValueConverter<IOptionalValue<TimeSpan>, long?> OptionalTimeSpanTicks = new(
        value => OptionalStructValue(value, duration => duration.Ticks),
        value => value.HasValue ? Optional.From(TimeSpan.FromTicks(value.Value)) : Optional.Missing<TimeSpan>());

    public static readonly ValueComparer<IOptionalValue<TimeSpan>> OptionalTimeSpanComparer = OptionalComparer<IOptionalValue<TimeSpan>, long?>(
        value => OptionalStructValue(value, duration => duration.Ticks),
        value => value.HasValue ? Optional.From(TimeSpan.FromTicks(value.Value)) : Optional.Missing<TimeSpan>());

    private static ValueComparer<TModel> OptionalComparer<TModel, TProvider>(
        Func<TModel, TProvider> convert,
        Func<TProvider, TModel> convertBack)
        where TModel : notnull
    {
        return new ValueComparer<TModel>(
            (left, right) => EqualityComparer<TProvider>.Default.Equals(convert(left!), convert(right!)),
            value => OptionalHash(convert(value!)),
            value => convertBack(convert(value!)));
    }

    private static IOptionalValue<CoverImage> OptionalCoverImageValue(string? value)
    {
        if (value is null)
        {
            return Optional.Missing<CoverImage>();
        }

        CoverImageStorageModel? model = JsonSerializer.Deserialize<CoverImageStorageModel>(value, CoverImageJsonOptions);
        return model is null
            ? Optional.Missing<CoverImage>()
            : Optional.From(CoverImage.FromStoredMetadata(
                model.StorageKey,
                model.ContentType,
                model.OriginalFileName,
                model.SizeBytes,
                model.SourceType));
    }

    private static string CoverImageToMetadataJson(CoverImage coverImage)
    {
        return JsonSerializer.Serialize(
            new CoverImageStorageModel(
                coverImage.StorageKey,
                coverImage.ContentType,
                coverImage.OriginalFileName,
                coverImage.SizeBytes,
                coverImage.SourceType),
            CoverImageJsonOptions);
    }

    private static int OptionalHash<TProvider>(TProvider value)
    {
        return value is null ? 0 : EqualityComparer<TProvider>.Default.GetHashCode(value);
    }

    private static TValue? OptionalStructValue<TOptional, TValue>(
        IOptionalValue<TOptional> optionalValue,
        Func<TOptional, TValue> selector)
        where TOptional : notnull
        where TValue : struct
    {
        return optionalValue is PresentOptionalValue<TOptional> present
            ? selector(present.Value)
            : null;
    }

    private static IOptionalValue<string> OptionalStringValue(string? value)
    {
        return value is null ? Optional.Missing<string>() : Optional.From(value);
    }

    private static string? OptionalStringValue<TOptional>(
        IOptionalValue<TOptional> optionalValue,
        Func<TOptional, string> selector)
        where TOptional : notnull
    {
        return optionalValue is PresentOptionalValue<TOptional> present
            ? selector(present.Value)
            : null;
    }

    private static readonly JsonSerializerOptions CoverImageJsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record CoverImageStorageModel(
        string StorageKey,
        string ContentType,
        string OriginalFileName,
        long SizeBytes,
        string SourceType);
}
