namespace Cratebase.Api.Features.Admin;

public sealed record CreateUserRequest(string Email, string Password, bool IsAdmin);
