using ModularMonolith.Domain.Common;

namespace ModularMonolith.Domain.Modules.Catalog;

/// <summary>
/// A reference-data entity owned by the Catalog module. It exists mainly to show
/// that a second module follows the same layering and slices as Jobs — modules
/// are isolated, not entangled.
/// </summary>
public sealed class CrewRole : Entity
{
    private CrewRole()
    {
    }

    private CrewRole(Guid id, string name)
    {
        Id = id;
        Name = name;
    }

    public string Name { get; private set; } = string.Empty;

    public static CrewRole Create(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required.", nameof(name));
        }

        return new CrewRole(Guid.NewGuid(), name.Trim());
    }
}
