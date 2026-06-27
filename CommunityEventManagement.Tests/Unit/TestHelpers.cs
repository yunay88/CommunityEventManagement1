using CommunityEventManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CommunityEventManagement.Tests.Unit;

/// <summary>
/// Shared helper for test files using the EF Core InMemory provider.
/// The InMemory provider does NOT support transactions — calls to
/// BeginTransactionAsync raise TransactionIgnoredWarning. We silence
/// that warning here so tests can run. The production code path
/// (SQLite) is unchanged.
/// </summary>
public static class TestDbContextFactory
{
    public static DbContextOptions<ApplicationDbContext> CreateOptions(string dbName)
    {
        return new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
    }
}