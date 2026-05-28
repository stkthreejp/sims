using Microsoft.EntityFrameworkCore;
using SIMS.Application.Services;
using SIMS.Domain.Entities.Accounting;
using SIMS.Infrastructure.Data;
using Xunit;

namespace SIMS.Application.Tests.Services;

public class LedgerServiceTests
{
    [Fact]
    public async Task ReverseTransactionGroupAsync_CreatesReversalRowsAndVoidsOriginals()
    {
        await using var db = CreateDb();
        var transactionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var effectiveDate = new DateOnly(2026, 5, 28);

        db.AddRange(
            CreateLedgerRow(transactionId, debit: 100m, credit: 0m, userId: userId),
            CreateLedgerRow(transactionId, debit: 0m, credit: 100m, userId: userId));
        await db.SaveChangesAsync();

        var service = new LedgerService(new DbServiceProvider(db));

        var reversalId = await service.ReverseTransactionGroupAsync(
            transactionId,
            "Void invoice",
            userId,
            effectiveDate);

        var rows = await db.Set<LedgerTransaction>()
            .Where(t => t.TransactionId == transactionId || t.TransactionId == reversalId)
            .ToListAsync();
        var originals = rows.Where(t => t.TransactionId == transactionId).ToList();
        var reversals = rows.Where(t => t.TransactionId == reversalId).ToList();

        Assert.Equal(2, originals.Count);
        Assert.All(originals, row =>
        {
            Assert.Equal("Voided", row.PostingStatus);
            Assert.Equal(reversalId, row.VoidedByTransactionId);
            Assert.Equal(userId, row.VoidedBy);
            Assert.Equal("Void invoice", row.VoidReason);
            Assert.NotNull(row.VoidedAt);
        });

        Assert.Equal(2, reversals.Count);
        Assert.All(reversals, row =>
        {
            Assert.Equal("Reversal", row.PostingStatus);
            Assert.Equal(transactionId, row.ReversesTransactionId);
            Assert.Equal(effectiveDate, row.EffectiveDate);
            Assert.Equal(userId, row.CreatedBy);
            Assert.Equal("Void invoice", row.VoidReason);
        });
        Assert.Equal(originals.Sum(t => t.Credit), reversals.Sum(t => t.Debit));
        Assert.Equal(originals.Sum(t => t.Debit), reversals.Sum(t => t.Credit));
    }

    [Fact]
    public async Task SaveChangesAsync_StillRejectsLedgerAmountMutation()
    {
        await using var db = CreateDb();
        var transactionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        db.Add(CreateLedgerRow(transactionId, debit: 100m, credit: 0m, userId: userId));
        await db.SaveChangesAsync();

        var row = await db.Set<LedgerTransaction>().SingleAsync();
        row.Debit = 200m;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        Assert.Contains("LedgerTransaction rows are immutable", ex.Message);
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private static LedgerTransaction CreateLedgerRow(Guid transactionId, decimal debit, decimal credit, Guid userId)
        => new()
        {
            TransactionId = transactionId,
            EffectiveDate = new DateOnly(2026, 5, 1),
            AccountId = debit > 0 ? 1000 : 2000,
            Debit = debit,
            Credit = credit,
            SourceType = "Invoice",
            SourceId = 123,
            Memo = "Test invoice",
            CreatedBy = userId,
            PostedAt = DateTime.UtcNow,
            PostingStatus = "Posted"
        };

    private sealed class DbServiceProvider : IServiceProvider
    {
        private readonly DbContext _db;

        public DbServiceProvider(DbContext db) => _db = db;

        public object? GetService(Type serviceType)
            => serviceType == typeof(DbContext) ? _db : null;
    }
}
