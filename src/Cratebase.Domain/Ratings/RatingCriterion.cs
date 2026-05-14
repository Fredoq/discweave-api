using Cratebase.Domain.SharedKernel.Errors;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Domain.SharedKernel.Interfaces;
using Cratebase.Domain.SharedKernel.Validation;

namespace Cratebase.Domain.Ratings;

public sealed class RatingCriterion : IEntity<RatingCriterionId>
{
    private readonly List<RatingCriterionTarget> _targets = [];

    private RatingCriterion()
    {
        Code = string.Empty;
        Name = string.Empty;
    }

    private RatingCriterion(
        RatingCriterionId id,
        CollectionId collectionId,
        string code,
        string name,
        CreationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        Id = id;
        CollectionId = collectionId;
        Code = Guard.RequiredText(code, nameof(code), "rating_criterion.code_required");
        Name = Guard.RequiredText(name, nameof(name), "rating_criterion.name_required");
        SortOrder = options.SortOrder;
        IsActive = true;
        IsBuiltin = options.IsBuiltin;
        IsProtected = options.IsProtected;
        ReplaceTargetTypes(options.TargetTypes, allowProtectedChange: true);
    }

    public RatingCriterionId Id { get; private set; }

    public CollectionId CollectionId { get; private set; }

    public string Code { get; private set; }

    public string Name { get; private set; }

    public int SortOrder { get; private set; }

    public bool IsActive { get; private set; }

    public bool IsBuiltin { get; private set; }

    public bool IsProtected { get; private set; }

    public IReadOnlyList<RatingTargetType> TargetTypes => [.. _targets.Select(target => target.TargetType)];

    public static RatingCriterion Create(
        RatingCriterionId id,
        CollectionId collectionId,
        string code,
        string name,
        IReadOnlyList<RatingTargetType> targetTypes,
        int sortOrder)
    {
        return new RatingCriterion(
            id,
            collectionId,
            code,
            name,
            new CreationOptions
            {
                TargetTypes = targetTypes,
                SortOrder = sortOrder,
                IsBuiltin = false,
                IsProtected = false
            });
    }

    public static RatingCriterion CreateProtected(
        RatingCriterionId id,
        CollectionId collectionId,
        string code,
        string name,
        IReadOnlyList<RatingTargetType> targetTypes,
        int sortOrder)
    {
        return new RatingCriterion(
            id,
            collectionId,
            code,
            name,
            new CreationOptions
            {
                TargetTypes = targetTypes,
                SortOrder = sortOrder,
                IsBuiltin = true,
                IsProtected = true
            });
    }

    public void Update(string name, IReadOnlyList<RatingTargetType> targetTypes, int sortOrder, bool isActive)
    {
        ArgumentNullException.ThrowIfNull(targetTypes);

        Name = Guard.RequiredText(name, nameof(name), "rating_criterion.name_required");
        SortOrder = sortOrder;

        if (!isActive)
        {
            Deactivate();
        }
        else
        {
            Activate();
        }

        ReplaceTargetTypes(targetTypes, allowProtectedChange: false);
    }

    public bool AppliesTo(RatingTargetType targetType)
    {
        return _targets.Any(target => target.TargetType == Guard.DefinedEnum(targetType, nameof(targetType), "rating_target.type_invalid"));
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        if (IsProtected)
        {
            throw new DomainException("rating_criterion.protected", "Protected rating criteria cannot be deactivated");
        }

        IsActive = false;
    }

    private void ReplaceTargetTypes(IReadOnlyList<RatingTargetType> targetTypes, bool allowProtectedChange)
    {
        ArgumentNullException.ThrowIfNull(targetTypes);

        if (IsProtected && !allowProtectedChange && !TargetTypes.SequenceEqual(targetTypes))
        {
            throw new DomainException("rating_criterion.protected", "Protected rating criteria cannot change target types");
        }

        if (targetTypes.Count == 0)
        {
            throw new DomainException("rating_criterion.target_required", "Rating criterion must target at least one entity type");
        }

        var seen = new HashSet<RatingTargetType>();
        foreach (RatingTargetType targetType in targetTypes)
        {
            RatingTargetType normalized = Guard.DefinedEnum(targetType, nameof(targetTypes), "rating_target.type_invalid");
            if (!seen.Add(normalized))
            {
                throw new DomainException("rating_criterion.target_duplicate", "Rating criterion target types must be unique");
            }
        }

        _targets.Clear();
        _targets.AddRange(targetTypes.Select(RatingCriterionTarget.Create));
    }

    private sealed class CreationOptions
    {
        public required IReadOnlyList<RatingTargetType> TargetTypes { get; init; }

        public int SortOrder { get; init; }

        public bool IsBuiltin { get; init; }

        public bool IsProtected { get; init; }
    }
}
