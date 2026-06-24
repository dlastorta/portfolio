using AutoMapper;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ModularMonolith.Application.Modules.Catalog;
using ModularMonolith.Application.Modules.Jobs;
using ModularMonolith.Domain.Abstractions;
using ModularMonolith.Infrastructure.Persistence;
using ModularMonolith.Infrastructure.Persistence.Repositories;

namespace ModularMonolith.UnitTests.Common;

/// <summary>
/// Wires a real DbContext (SQLite in-memory), repositories, unit of work, AutoMapper,
/// and a recording event dispatcher — so handler tests exercise the real persistence
/// and mapping path, not mocks of it.
/// </summary>
public sealed class ApplicationTestHarness : IDisposable
{
    private readonly SqliteConnection _connection;

    public ApplicationTestHarness()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        Dispatcher = new RecordingDomainEventDispatcher();
        Context = new ApplicationDbContext(options, Dispatcher);
        Context.Database.EnsureCreated();

        UnitOfWork = new UnitOfWork(
            Context,
            new JobRepository(Context),
            new CrewRoleRepository(Context));

        Mapper = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<JobMappingProfile>();
            cfg.AddProfile<CrewRoleMappingProfile>();
        }).CreateMapper();
    }

    public ApplicationDbContext Context { get; }

    public IUnitOfWork UnitOfWork { get; }

    public IMapper Mapper { get; }

    public RecordingDomainEventDispatcher Dispatcher { get; }

    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }
}
