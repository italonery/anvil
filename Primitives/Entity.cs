namespace Anvil.Primitives;

public interface IEntity
{
    DateTimeOffset Created { get; }
    DateTimeOffset? Updated { get; }
}

public interface IEntity<out TId> : IEntity where TId : notnull
{
    TId Id { get; }
}

public abstract class Entity<TId> : IEntity<TId> where TId : notnull
{
    public TId Id { get; protected init; } = default!;
    public DateTimeOffset Created { get; protected init; }
    public DateTimeOffset? Updated { get; protected init; }
}
