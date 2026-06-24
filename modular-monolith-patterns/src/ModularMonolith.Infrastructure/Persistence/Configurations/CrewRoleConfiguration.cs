using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ModularMonolith.Domain.Modules.Catalog;

namespace ModularMonolith.Infrastructure.Persistence.Configurations;

public sealed class CrewRoleConfiguration : IEntityTypeConfiguration<CrewRole>
{
    public void Configure(EntityTypeBuilder<CrewRole> builder)
    {
        builder.ToTable("CrewRoles");

        builder.HasKey(crewRole => crewRole.Id);

        builder.Property(crewRole => crewRole.Name)
            .IsRequired()
            .HasMaxLength(100);
    }
}
