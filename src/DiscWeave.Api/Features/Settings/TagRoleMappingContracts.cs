namespace DiscWeave.Api.Features.Settings;

public sealed record TagRoleMappingRequest(
    string CreditRoleCode,
    string TagField,
    int? SortOrder,
    bool? IsActive);

public sealed record TagRoleMappingResponse(
    Guid Id,
    string CreditRoleCode,
    string TagField,
    int SortOrder,
    bool IsActive,
    bool IsBuiltin);
