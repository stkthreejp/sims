using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMS.Application.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Entities.Accounting;
using SIMS.Domain.Enums;
using SIMS.Infrastructure.Data;
using Xunit;

namespace SIMS.Application.Tests.Services;

public class ReportServiceTests
{
    [Fact]
    public async Task GetInvoiceTotalsByProgramAsync_GroupsPostedInvoiceTotalsByPolicyProgram()
    {
        await using var db = CreateDb();
        var longleaf = new ProgramConfiguration { Name = "Longleaf", Code = "LONGLEAF", IsActive = true };
        var shuttleBee = new ProgramConfiguration { Name = "ShuttleBee", Code = "SHUTTLEBEE", IsActive = true };
        db.AddRange(longleaf, shuttleBee);

        var longleafPolicy = PolicyFor(longleaf.Id);
        var shuttleBeePolicy = PolicyFor(shuttleBee.Id);
        var unassignedPolicy = PolicyFor(null);
        var longleafTxn = TransactionFor(longleafPolicy);
        var shuttleBeeTxn = TransactionFor(shuttleBeePolicy);
        var unassignedTxn = TransactionFor(unassignedPolicy);

        db.AddRange(
            longleafPolicy,
            shuttleBeePolicy,
            unassignedPolicy,
            longleafTxn,
            shuttleBeeTxn,
            unassignedTxn,
            InvoiceFor(longleafTxn.Id, "INV-LL-1", 1000m, 100m, 1100m, 150m, 50m),
            InvoiceFor(longleafTxn.Id, "INV-LL-2", 250m, 25m, 275m, 30m, 10m),
            InvoiceFor(shuttleBeeTxn.Id, "INV-SB-1", 500m, 50m, 550m, 75m, 25m),
            InvoiceFor(unassignedTxn.Id, "INV-UN-1", 300m, 30m, 330m, 45m, 15m),
            InvoiceFor(longleafTxn.Id, "INV-VOID", 999m, 99m, 1098m, 99m, 99m, status: "Voided"));
        await db.SaveChangesAsync();

        var reports = new ReportService(new ServiceCollection().AddSingleton<DbContext>(db).BuildServiceProvider());

        var result = await reports.GetInvoiceTotalsByProgramAsync();

        Assert.Equal(3, result.Rows.Count);
        var longleafRow = Assert.Single(result.Rows, r => r.ProgramId == longleaf.Id);
        Assert.Equal("Longleaf", longleafRow.ProgramName);
        Assert.Equal("LONGLEAF", longleafRow.ProgramCode);
        Assert.Equal(2, longleafRow.InvoiceCount);
        Assert.Equal(1250m, longleafRow.GrossPremium);
        Assert.Equal(125m, longleafRow.TotalFees);
        Assert.Equal(1375m, longleafRow.TotalAmount);
        Assert.Equal(180m, longleafRow.CommissionAmount);
        Assert.Equal(60m, longleafRow.AgentCommissionAmount);
        Assert.Equal(120m, longleafRow.NetRetained);

        var shuttleBeeRow = Assert.Single(result.Rows, r => r.ProgramId == shuttleBee.Id);
        Assert.Equal(550m, shuttleBeeRow.TotalAmount);

        var unassignedRow = Assert.Single(result.Rows, r => r.ProgramId == null);
        Assert.Equal("Unassigned", unassignedRow.ProgramName);
        Assert.Equal(330m, unassignedRow.TotalAmount);
    }

    [Fact]
    public async Task GetInvoiceTotalsByProgramAsync_FiltersToSelectedProgram()
    {
        await using var db = CreateDb();
        var longleaf = new ProgramConfiguration { Name = "Longleaf", Code = "LONGLEAF", IsActive = true };
        var shuttleBee = new ProgramConfiguration { Name = "ShuttleBee", Code = "SHUTTLEBEE", IsActive = true };
        db.AddRange(longleaf, shuttleBee);

        var longleafPolicy = PolicyFor(longleaf.Id);
        var shuttleBeePolicy = PolicyFor(shuttleBee.Id);
        var longleafTxn = TransactionFor(longleafPolicy);
        var shuttleBeeTxn = TransactionFor(shuttleBeePolicy);

        db.AddRange(
            longleafPolicy,
            shuttleBeePolicy,
            longleafTxn,
            shuttleBeeTxn,
            InvoiceFor(longleafTxn.Id, "INV-LL-1", 1000m, 100m, 1100m, 150m, 50m),
            InvoiceFor(shuttleBeeTxn.Id, "INV-SB-1", 500m, 50m, 550m, 75m, 25m));
        await db.SaveChangesAsync();

        var reports = new ReportService(new ServiceCollection().AddSingleton<DbContext>(db).BuildServiceProvider());

        var result = await reports.GetInvoiceTotalsByProgramAsync(longleaf.Id);

        var row = Assert.Single(result.Rows);
        Assert.Equal(longleaf.Id, row.ProgramId);
        Assert.Equal("Longleaf", row.ProgramName);
        Assert.Equal(1100m, row.TotalAmount);
    }

    private static Policy PolicyFor(Guid? programId) => new()
    {
        Id = Guid.NewGuid(),
        PolicyNumber = Guid.NewGuid().ToString("N"),
        SubmissionId = Guid.NewGuid(),
        BoundQuoteId = Guid.NewGuid(),
        ProgramId = programId,
        CarrierId = Guid.NewGuid(),
        LineOfBusiness = PolicyLineOfBusiness.InlandMarine,
        EffectiveDate = new DateOnly(2026, 1, 1),
        ExpirationDate = new DateOnly(2027, 1, 1),
        BoundDate = new DateOnly(2026, 1, 1)
    };

    private static PolicyTransaction TransactionFor(Policy policy) => new()
    {
        Id = Guid.NewGuid(),
        PolicyId = policy.Id,
        TransactionType = TransactionType.NewBusiness,
        Status = PolicyTransactionStatus.Issued,
        TransactionNumber = Guid.NewGuid().ToString("N"),
        EffectiveDate = policy.EffectiveDate,
        ProcessedById = Guid.NewGuid()
    };

    private static Invoice InvoiceFor(
        Guid policyTransactionId,
        string invoiceNumber,
        decimal grossPremium,
        decimal totalFees,
        decimal totalAmount,
        decimal commissionAmount,
        decimal agentCommissionAmount,
        string status = "Posted") => new()
    {
        InvoiceNumber = invoiceNumber,
        PolicyTransactionId = policyTransactionId,
        EffectiveDate = new DateOnly(2026, 1, 1),
        InvoiceDate = new DateOnly(2026, 1, 1),
        GrossPremium = grossPremium,
        TotalFees = totalFees,
        TotalAmount = totalAmount,
        CommissionAmount = commissionAmount,
        AgentCommissionAmount = agentCommissionAmount,
        LedgerTransactionId = Guid.NewGuid(),
        CreatedBy = Guid.NewGuid(),
        Status = status
    };

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }
}
