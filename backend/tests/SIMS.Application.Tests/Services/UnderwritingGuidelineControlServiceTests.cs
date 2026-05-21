using Microsoft.EntityFrameworkCore;
using SIMS.Application.DTOs.Underwriting;
using SIMS.Application.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Enums;
using SIMS.Infrastructure.Data;
using Xunit;

namespace SIMS.Application.Tests.Services;

public class UnderwritingGuidelineControlServiceTests
{
    [Fact]
    public async Task ApproveControlAsync_WritesAuditWithoutSerializingEfNavigationGraph()
    {
        await using var db = CreateDb();
        var program = new ProgramConfiguration
        {
            Name = "Lloyds GL",
            Code = "LLOYDS-GL",
            IsActive = true
        };
        db.Add(program);
        await db.SaveChangesAsync();

        var service = new UnderwritingGuidelineControlService(db);
        var document = await service.CreateDocumentAsync(new CreateUnderwritingGuidelineDocumentRequest(
            ProgramName: "Ignored",
            CarrierId: null,
            LineOfBusiness: PolicyLineOfBusiness.InlandMarine,
            StateCode: "ALL",
            Title: "Lloyds GL Guidelines",
            SourceFileName: null,
            SourceBlobName: null,
            Notes: null,
            ProgramId: program.Id), Guid.NewGuid());

        var controls = await service.AddProposedControlsAsync(document.Value!.Id, new AddProposedUnderwritingControlsRequest([
            new CreateUnderwritingGuidelineControlRequest(
                UnderwritingControlItemType.DocumentChecklistItem,
                UnderwritingControlStage.Bind,
                UnderwritingControlSeverity.HardBlock,
                "signed-application",
                "Signed application",
                "Signed application required before bind.",
                null,
                true,
                true,
                "underwriting.clearance.override",
                "Page 1",
                0.8m,
                10)
        ]), Guid.NewGuid());

        var result = await service.ApproveControlAsync(controls.Value!.Single().Id, Guid.NewGuid(), "Looks right");

        Assert.True(result.IsSuccess);
        Assert.Equal(UnderwritingControlStatus.Approved, result.Value!.Status);
        Assert.Contains(await db.Set<UnderwritingGuidelineAuditLog>().ToListAsync(), a => a.Action == "ControlApproved");
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }
}
