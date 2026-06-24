namespace ModularMonolith.Domain.Common;

/// <summary>Base type for entities with identity.</summary>
public abstract class Entity
{
    public Guid Id { get; protected set; }
}
