namespace DiscWeave.Domain.SharedKernel.Interfaces;

public interface IEntity<out TId>
    where TId : notnull
{
    TId Id { get; }
}
