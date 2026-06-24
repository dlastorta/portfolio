using ModularMonolith.Domain.Modules.Jobs;
using Riok.Mapperly.Abstractions;

namespace ModularMonolith.Application.Modules.Jobs;

/// <summary>
/// Mapperly source-generated mapper. The partial methods are implemented at compile time —
/// no reflection, and a missing/again-mismatched member is a build error, not a runtime surprise.
/// The <see cref="JobStatus"/> enum maps to its name string automatically.
/// </summary>
[Mapper]
public partial class JobMapper
{
    // DomainEvents is an aggregate concern with no DTO counterpart — don't map it.
    [MapperIgnoreSource(nameof(Job.DomainEvents))]
    public partial JobDto ToDto(Job job);

    public partial List<JobDto> ToDtos(IEnumerable<Job> jobs);
}
