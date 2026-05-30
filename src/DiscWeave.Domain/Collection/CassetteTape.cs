using DiscWeave.Domain.SharedKernel.Validation;

namespace DiscWeave.Domain.Collection;

public sealed record CassetteTape : IMedium
{
    private CassetteTape(string code, string tapeType)
    {
        Code = code;
        TapeType = tapeType;
    }

    public string Code { get; }

    public string TapeType { get; }

    public string Description => TapeType;

    public static CassetteTape Create(string tapeType)
    {
        return Create("cassette", tapeType);
    }

    public static CassetteTape Create(string code, string tapeType)
    {
        return new CassetteTape(
            Guard.RequiredText(code, nameof(code), "medium.type_required"),
            Guard.RequiredText(tapeType, nameof(tapeType), "cassette_tape.type_required"));
    }
}
