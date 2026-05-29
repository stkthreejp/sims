using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SIMS.Application.DTOs.Accounting;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities.Accounting;
using SIMS.Infrastructure.Data;
using SIMS.Infrastructure.Workers;
using Xunit;

namespace SIMS.Application.Tests.Services;

public class QboSyncRetryWorkerTests
{
    [Fact]
    public async Task RunAsync_DoesNotMarkSyncDoneWhenResyncReturnsFailedRollup()
    {
        await using var fixture = await QboRetryFixture.CreateAsync(new FailedRollupService("QBO rejected journal"));
        await SeedPendingQboSyncAsync(fixture.Db);
        var worker = CreateWorker(fixture.Services);

        using var scope = fixture.Services.CreateScope();
        await RunWorkerOnceAsync(worker, scope.ServiceProvider);

        fixture.Db.ChangeTracker.Clear();
        var sync = await fixture.Db.PendingQboSyncs.SingleAsync();
        Assert.NotEqual("Done", sync.Status);
        Assert.Equal("Retrying", sync.Status);
        Assert.Equal(1, sync.AttemptCount);
        Assert.Contains("QBO rejected journal", sync.LastError);
        Assert.NotNull(sync.NextRetryAt);
    }

    [Fact]
    public async Task RunAsync_DoesNotProcessSyncAlreadyClaimedByAnotherWorker()
    {
        var rollupService = new BlockingRollupService();
        await using var fixture = await QboRetryFixture.CreateAsync(rollupService);
        await SeedPendingQboSyncAsync(fixture.Db);
        var worker = CreateWorker(fixture.Services);

        using var firstScope = fixture.Services.CreateScope();
        using var secondScope = fixture.Services.CreateScope();
        var firstRun = RunWorkerOnceAsync(worker, firstScope.ServiceProvider);
        await rollupService.FirstCallStarted.WaitAsync(TimeSpan.FromSeconds(5));

        var secondRun = RunWorkerOnceAsync(worker, secondScope.ServiceProvider);
        await rollupService.WaitForAdditionalCallOrTimeoutAsync(TimeSpan.FromMilliseconds(300));

        Assert.Equal(1, rollupService.CallCount);

        rollupService.Release();
        await Task.WhenAll(firstRun, secondRun);
    }

    [Fact]
    public async Task RunAsync_ReclaimsStaleProcessingSync()
    {
        var rollupService = new SuccessfulRollupService();
        await using var fixture = await QboRetryFixture.CreateAsync(rollupService);
        await SeedPendingQboSyncAsync(fixture.Db, status: "Processing", attemptCount: 1, updatedAt: DateTime.UtcNow.AddMinutes(-20));
        var worker = CreateWorker(fixture.Services);

        using var scope = fixture.Services.CreateScope();
        await RunWorkerOnceAsync(worker, scope.ServiceProvider);

        fixture.Db.ChangeTracker.Clear();
        var sync = await fixture.Db.PendingQboSyncs.SingleAsync();
        Assert.Equal("Done", sync.Status);
        Assert.Equal(2, sync.AttemptCount);
        Assert.Equal(1, rollupService.CallCount);
    }

    private static QboSyncRetryWorker CreateWorker(IServiceProvider services)
        => new(
            services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<QboSyncRetryWorker>.Instance);

    private static async Task SeedPendingQboSyncAsync(
        ApplicationDbContext db,
        string status = "Pending",
        int attemptCount = 0,
        DateTime? updatedAt = null)
    {
        var rollup = new JournalEntryRollup
        {
            PeriodYear = 2026,
            PeriodMonth = 5,
            DriverType = "QBO",
            Status = "Failed",
            CreatedBy = Guid.NewGuid(),
        };
        db.Add(new PendingQboSync
        {
            Rollup = rollup,
            Status = status,
            AttemptCount = attemptCount,
            NextRetryAt = DateTime.UtcNow.AddMinutes(-1),
            UpdatedAt = updatedAt ?? DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    private static Task RunWorkerOnceAsync(QboSyncRetryWorker worker, IServiceProvider services)
    {
        var method = typeof(QboSyncRetryWorker).GetMethod("RunAsync", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(nameof(QboSyncRetryWorker), "RunAsync");
        return (Task)method.Invoke(worker, [services, CancellationToken.None])!;
    }

    private sealed class FailedRollupService(string errorMessage) : IRollupService
    {
        public Task<RollupDto> ResyncAsync(long rollupId, Guid userId, CancellationToken ct = default)
            => Task.FromResult(new RollupDto(
                rollupId,
                2026,
                5,
                "QBO",
                "Failed",
                0,
                0,
                null,
                null,
                errorMessage,
                DateTime.UtcNow,
                DateTime.UtcNow));

        public Task<RollupDto> RollupPeriodAsync(int year, int month, string driverType, Guid userId, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<RollupSummaryDto>> GetRollupsAsync(CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<RollupDto?> GetRollupAsync(long id, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<string> GetDownloadUrlAsync(long rollupId, CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private sealed class BlockingRollupService : IRollupService
    {
        private readonly TaskCompletionSource _firstCallStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task FirstCallStarted => _firstCallStarted.Task;
        public int CallCount => Volatile.Read(ref _callCount);

        private int _callCount;

        public async Task<RollupDto> ResyncAsync(long rollupId, Guid userId, CancellationToken ct = default)
        {
            Interlocked.Increment(ref _callCount);
            _firstCallStarted.TrySetResult();
            await _release.Task.WaitAsync(ct);
            return new RollupDto(
                rollupId,
                2026,
                5,
                "QBO",
                "Exported",
                0,
                0,
                "qbo-1",
                null,
                null,
                DateTime.UtcNow,
                DateTime.UtcNow);
        }

        public async Task WaitForAdditionalCallOrTimeoutAsync(TimeSpan timeout)
        {
            var until = DateTime.UtcNow.Add(timeout);
            while (DateTime.UtcNow < until && CallCount < 2)
                await Task.Delay(10);
        }

        public void Release() => _release.TrySetResult();

        public Task<RollupDto> RollupPeriodAsync(int year, int month, string driverType, Guid userId, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<RollupSummaryDto>> GetRollupsAsync(CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<RollupDto?> GetRollupAsync(long id, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<string> GetDownloadUrlAsync(long rollupId, CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private sealed class SuccessfulRollupService : IRollupService
    {
        private int _callCount;
        public int CallCount => Volatile.Read(ref _callCount);

        public Task<RollupDto> ResyncAsync(long rollupId, Guid userId, CancellationToken ct = default)
        {
            Interlocked.Increment(ref _callCount);
            return Task.FromResult(new RollupDto(
                rollupId,
                2026,
                5,
                "QBO",
                "Exported",
                0,
                0,
                "qbo-1",
                null,
                null,
                DateTime.UtcNow,
                DateTime.UtcNow));
        }

        public Task<RollupDto> RollupPeriodAsync(int year, int month, string driverType, Guid userId, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<RollupSummaryDto>> GetRollupsAsync(CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<RollupDto?> GetRollupAsync(long id, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<string> GetDownloadUrlAsync(long rollupId, CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private sealed class QboRetryFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _anchorConnection;

        private QboRetryFixture(SqliteConnection anchorConnection, ServiceProvider services, ApplicationDbContext db)
        {
            _anchorConnection = anchorConnection;
            Services = services;
            Db = db;
        }

        public ServiceProvider Services { get; }
        public ApplicationDbContext Db { get; }

        public static async Task<QboRetryFixture> CreateAsync(IRollupService rollupService)
        {
            var connectionString = $"Data Source={Guid.NewGuid():N};Mode=Memory;Cache=Shared";
            var anchorConnection = new SqliteConnection(connectionString);
            await anchorConnection.OpenAsync();
            await CreateSchemaAsync(anchorConnection);

            var services = new ServiceCollection()
                .AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connectionString))
                .AddSingleton<IRollupService>(rollupService)
                .BuildServiceProvider();

            var db = services.GetRequiredService<ApplicationDbContext>();
            return new QboRetryFixture(anchorConnection, services, db);
        }

        private static async Task CreateSchemaAsync(SqliteConnection connection)
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                CREATE TABLE journal_entry_rollups (
                    Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    TenantId INTEGER NOT NULL,
                    PeriodYear INTEGER NOT NULL,
                    PeriodMonth INTEGER NOT NULL,
                    DriverType TEXT NOT NULL,
                    Status TEXT NOT NULL,
                    ExternalId TEXT NULL,
                    BlobUri TEXT NULL,
                    ErrorMessage TEXT NULL,
                    CreatedBy TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    CompletedAt TEXT NULL
                );

                CREATE TABLE pending_qbo_syncs (
                    Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    TenantId INTEGER NOT NULL,
                    RollupId INTEGER NOT NULL,
                    Status TEXT NOT NULL,
                    AttemptCount INTEGER NOT NULL,
                    NextRetryAt TEXT NULL,
                    LastError TEXT NULL,
                    CreatedAt TEXT NOT NULL,
                    UpdatedAt TEXT NOT NULL,
                    CONSTRAINT FK_pending_qbo_syncs_journal_entry_rollups_RollupId
                        FOREIGN KEY (RollupId) REFERENCES journal_entry_rollups (Id) ON DELETE CASCADE
                );

                CREATE INDEX ix_pending_qbo_syncs_status ON pending_qbo_syncs (Status);
                CREATE INDEX ix_pending_qbo_syncs_next_retry ON pending_qbo_syncs (NextRetryAt);
                """;
            await command.ExecuteNonQueryAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await Services.DisposeAsync();
            await _anchorConnection.DisposeAsync();
        }
    }
}
