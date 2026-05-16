using System.Reflection;
using Cratebase.Domain.Catalog;
using Cratebase.Domain.Collection;
using Cratebase.Domain.Credits;
using Cratebase.Domain.Ratings;
using Cratebase.Domain.Relations;
using Cratebase.Domain.Settings;

namespace Cratebase.Domain.Tests;

public sealed class DomainModelShapeTests
{
    [Fact]
    public void Domain_types_keep_a_small_public_api()
    {
        Type[] domainTypes =
        [
            .. GetPublicDomainTypes()
        ];

        string[] violations =
        [
            .. domainTypes
            .Select(type => new
            {
                Type = type,
                PropertyCount = CountPublicInstanceProperties(type),
                MethodCount = CountPublicMethods(type),
                MaximumPropertyCount = GetMaximumPublicPropertyCount(type),
                MaximumMethodCount = GetMaximumPublicMethodCount(type)
            })
            .Where(result => result.PropertyCount > result.MaximumPropertyCount || result.MethodCount > result.MaximumMethodCount)
            .Select(result => $"{result.Type.FullName}: {result.PropertyCount} properties, {result.MethodCount} methods")
        ];

        Assert.Empty(violations);
    }

    [Fact]
    public void Domain_public_api_does_not_use_nullable_contracts()
    {
        Type[] domainTypes =
        [
            .. GetPublicDomainTypes()
        ];

        string[] violations =
        [
            .. domainTypes.SelectMany(type => NullablePropertyViolations(type)
                .Concat(NullableConstructorParameterViolations(type))
                .Concat(NullableParameterViolations(type))
                .Concat(NullableReturnViolations(type)))
        ];

        Assert.Empty(violations);
    }

    [Fact]
    public void Domain_choices_do_not_use_public_string_identity_or_open_string_factories()
    {
        Type[] domainTypes =
        [
            .. typeof(Release).Assembly.GetTypes()
                .Where(type =>
                    type is { IsPublic: true } &&
                    IsDomainNamespace(type.Namespace))
        ];
        Type[] choiceTypes =
        [
            typeof(ReleaseType),
            typeof(AudioFileFormat),
            typeof(ItemCondition),
            typeof(OwnershipStatus),
            typeof(CreditRole),
            typeof(ArtistRelationType),
            typeof(TrackRelationType)
        ];

        string[] violations =
        [
            .. domainTypes.SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(method => method.DeclaringType != typeof(RatingTargetTypeCodes))
                .Where(method => method.Name is "FromCode" or "FromDescription")
                .Select(method => $"{type.FullName}.{method.Name} is an open string factory")),
            .. choiceTypes.SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(property => property.PropertyType == typeof(string) && property.Name is "Code" or "Description")
                .Select(property => $"{type.FullName}.{property.Name} is public string identity"))
        ];

        Assert.Empty(violations);
    }

    [Fact]
    public void Private_parameterless_constructors_are_limited_to_EF_materialization_shapes()
    {
        string[] expectedTypes =
        [
            typeof(ArtistRelation).FullName!,
            typeof(Credit).FullName!,
            typeof(OwnedItem).FullName!,
            typeof(Release).FullName!,
            typeof(ReleaseLabel).FullName!,
            typeof(ReleaseSummary).FullName!,
            typeof(ReleaseTrack).FullName!,
            typeof(RatingCriterion).FullName!,
            typeof(RatingCriterionTarget).FullName!,
            typeof(RatingValue).FullName!,
            typeof(Track).FullName!,
            typeof(TrackRelation).FullName!,
            typeof(TrackPosition).FullName!,
            typeof(CollectionDictionaryEntry).FullName!
        ];
        string[] actualTypes =
        [
            .. typeof(Release).Assembly.GetTypes()
                .Where(type =>
                    type is { IsClass: true } &&
                    IsDomainNamespace(type.Namespace))
                .Where(HasPrivateParameterlessConstructor)
                .Select(type => type.FullName!)
                .Order(StringComparer.Ordinal)
        ];

        Assert.Equal(expectedTypes.Order(StringComparer.Ordinal), actualTypes);
    }

    [Fact]
    public void Private_setters_are_limited_to_EF_materialization_shapes()
    {
        string[] expectedTypes =
        [
            typeof(Artist).FullName!,
            typeof(ArtistRelation).FullName!,
            typeof(Credit).FullName!,
            typeof(Label).FullName!,
            typeof(MusicCollection).FullName!,
            typeof(OwnedItem).FullName!,
            typeof(Release).FullName!,
            typeof(ReleaseLabel).FullName!,
            typeof(ReleaseTrack).FullName!,
            typeof(RatingCriterion).FullName!,
            typeof(RatingCriterionTarget).FullName!,
            typeof(RatingValue).FullName!,
            typeof(Track).FullName!,
            typeof(TrackRelation).FullName!,
            typeof(TrackPosition).FullName!,
            typeof(CollectionDictionaryEntry).FullName!
        ];
        string[] actualTypes =
        [
            .. typeof(Release).Assembly.GetTypes()
                .Where(type =>
                    type is { IsClass: true } &&
                    IsDomainNamespace(type.Namespace))
                .Where(HasPublicPropertyWithPrivateSetter)
                .Select(type => type.FullName!)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
        ];

        Assert.Equal(expectedTypes.Order(StringComparer.Ordinal), actualTypes);
    }

    private static IEnumerable<Type> GetPublicDomainTypes()
    {
        return typeof(Release).Assembly.GetTypes()
            .Where(type =>
                type is { IsPublic: true } &&
                (type.IsClass || type.IsValueType || type.IsInterface || type.IsEnum) &&
                IsDomainNamespace(type.Namespace))
            .OrderBy(type => type.FullName, StringComparer.Ordinal);
    }

    private static bool IsDomainNamespace(string? namespaceName)
    {
        return namespaceName != "Cratebase.Domain.Imports" &&
            (namespaceName == "Cratebase.Domain" ||
                namespaceName?.StartsWith("Cratebase.Domain.", StringComparison.Ordinal) == true);
    }

    private static int CountPublicInstanceProperties(Type type)
    {
        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Length;
    }

    private static int CountPublicMethods(Type type)
    {
        return type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Count(method =>
                !method.IsSpecialName &&
                !method.Name.StartsWith('<') &&
                method.Name is not nameof(Equals) and not nameof(GetHashCode) and not nameof(ToString));
    }

    private static int GetMaximumPublicPropertyCount(Type type)
    {
        return type == typeof(CollectionDictionaryEntry)
            ? 10
            : type == typeof(RatingCriterion)
            ? 9
            : type == typeof(Release)
            ? 9
            : type == typeof(Track) ||
            type == typeof(ArtistRelation)
            ? 6
            : 5;
    }

    private static int GetMaximumPublicMethodCount(Type type)
    {
        return type == typeof(CollectionDictionaryEntry) ||
            type == typeof(ArtistRelation)
            ? 8
            : type == typeof(RatingCriterion)
            ? 6
            : type == typeof(ReleaseMetadata)
                ? 7
                : type == typeof(Release)
            ? 10
            : type == typeof(Track)
                ? 8
                : 5;
    }

    private static IEnumerable<string> NullablePropertyViolations(Type type)
    {
        var nullabilityContext = new NullabilityInfoContext();

        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(property =>
                Nullable.GetUnderlyingType(property.PropertyType) is not null ||
                nullabilityContext.Create(property).ReadState == NullabilityState.Nullable)
            .Select(property => $"{type.FullName}.{property.Name} uses a nullable property type");
    }

    private static IEnumerable<string> NullableParameterViolations(Type type)
    {
        var nullabilityContext = new NullabilityInfoContext();

        return type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName && method.Name is not nameof(Equals))
            .SelectMany(method => method.GetParameters()
                .Where(parameter =>
                    Nullable.GetUnderlyingType(parameter.ParameterType) is not null ||
                    nullabilityContext.Create(parameter).ReadState == NullabilityState.Nullable ||
                    HasNullDefault(parameter))
                .Select(parameter => $"{type.FullName}.{method.Name} parameter {parameter.Name} uses a nullable contract"));
    }

    private static IEnumerable<string> NullableConstructorParameterViolations(Type type)
    {
        var nullabilityContext = new NullabilityInfoContext();

        return type.GetConstructors(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .SelectMany(constructor => constructor.GetParameters()
                .Where(parameter =>
                    Nullable.GetUnderlyingType(parameter.ParameterType) is not null ||
                    nullabilityContext.Create(parameter).ReadState == NullabilityState.Nullable ||
                    HasNullDefault(parameter))
                .Select(parameter => $"{type.FullName} constructor parameter {parameter.Name} uses a nullable contract"));
    }

    private static IEnumerable<string> NullableReturnViolations(Type type)
    {
        var nullabilityContext = new NullabilityInfoContext();

        return type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName && method.Name is not nameof(Equals))
            .Where(method =>
                Nullable.GetUnderlyingType(method.ReturnType) is not null ||
                nullabilityContext.Create(method.ReturnParameter).ReadState == NullabilityState.Nullable)
            .Select(method => $"{type.FullName}.{method.Name} returns a nullable contract");
    }

    private static bool HasNullDefault(ParameterInfo parameter)
    {
        return parameter.HasDefaultValue && parameter.DefaultValue is null;
    }

    private static bool HasPrivateParameterlessConstructor(Type type)
    {
        return type.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Any(constructor => constructor.IsPrivate && constructor.GetParameters().Length == 0);
    }

    private static bool HasPublicPropertyWithPrivateSetter(Type type)
    {
        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Any(property => property.SetMethod is { IsPrivate: true });
    }
}
