using DiscWeave.Domain.SharedKernel.Validation;

namespace DiscWeave.Domain.SharedKernel.Errors;

public sealed class DomainException : Exception
{
    public DomainException()
        : this("domain.error", "A domain rule was violated")
    {
    }

    public DomainException(string message)
        : this("domain.error", message)
    {
    }

    public DomainException(string message, Exception innerException)
        : this("domain.error", message, innerException)
    {
    }

    public DomainException(string code, string message)
        : base(message)
    {
        Code = Guard.RequiredText(code, nameof(code), "domain_exception.code_required");
    }

    public DomainException(string code, string message, Exception innerException)
        : base(message, innerException)
    {
        Code = Guard.RequiredText(code, nameof(code), "domain_exception.code_required");
    }

    public string Code { get; }
}
