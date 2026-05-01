using Cratebase.Domain.Catalog;
using Cratebase.Domain.Ratings;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Domain.SharedKernel.Optional;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Cratebase.Infrastructure.Persistence.Configurations;

internal static class PersistenceValueConverters
{
    public static readonly ValueConverter<ArtistId, Guid> ArtistId = new(
        id => id.Value,
        value => new ArtistId(value));

    public static readonly ValueConverter<LabelId, Guid> LabelId = new(
        id => id.Value,
        value => new LabelId(value));

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

    public static readonly ValueConverter<ArtistRelationId, Guid> ArtistRelationId = new(
        id => id.Value,
        value => new ArtistRelationId(value));

    public static readonly ValueConverter<TrackRelationId, Guid> TrackRelationId = new(
        id => id.Value,
        value => new TrackRelationId(value));

    public static readonly ValueConverter<IOptionalValue<CoverImage>, string?> OptionalCoverImage = new(
        value => OptionalStringValue(value, coverImage => coverImage.Path),
        value => OptionalCoverImageValue(value));

    public static readonly ValueComparer<IOptionalValue<CoverImage>> OptionalCoverImageComparer = OptionalComparer<IOptionalValue<CoverImage>, string?>(
        value => OptionalStringValue(value, coverImage => coverImage.Path),
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
        return value is null ? Optional.Missing<CoverImage>() : Optional.From(CoverImage.FromPath(value));
    }

    private static int OptionalHash<TProvider>(TProvider value)
    {
        return value is null ? 0 : EqualityComparer<TProvider>.Default.GetHashCode(value);
    }

    private static IOptionalValue<string> OptionalStringValue(string? value)
    {
        return value is null ? Optional.Missing<string>() : Optional.From(value);
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

    private static string? OptionalStringValue<TOptional>(
        IOptionalValue<TOptional> optionalValue,
        Func<TOptional, string> selector)
        where TOptional : notnull
    {
        return optionalValue is PresentOptionalValue<TOptional> present
            ? selector(present.Value)
            : null;
    }
}
