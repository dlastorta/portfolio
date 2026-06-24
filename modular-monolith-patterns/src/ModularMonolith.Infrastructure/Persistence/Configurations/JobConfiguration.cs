using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ModularMonolith.Domain.Modules.Jobs;

namespace ModularMonolith.Infrastructure.Persistence.Configurations;

public sealed class JobConfiguration : IEntityTypeConfiguration<Job>
{
    public void Configure(EntityTypeBuilder<Job> builder)
    {
        builder.ToTable("Jobs");

        builder.HasKey(job => job.Id);

        builder.Property(job => job.Title)
            .IsRequired()
            .HasMaxLength(200);

        // Persist the enum as readable text rather than an int.
        builder.Property(job => job.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(job => job.CreatedAtUtc)
            .IsRequired();

        // Domain events are an in-memory concern, never a column.
        builder.Ignore(job => job.DomainEvents);
    }
}
