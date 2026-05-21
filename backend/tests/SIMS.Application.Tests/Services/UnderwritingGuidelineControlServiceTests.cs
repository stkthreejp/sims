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

    [Fact]
    public async Task UpdateDocumentAsync_UpdatesDocumentAndControlScope()
    {
        await using var db = CreateDb();
        var carrier = new Carrier { Name = "Lloyds of London", IsActive = true };
        db.Add(carrier);
        await db.SaveChangesAsync();

        var service = new UnderwritingGuidelineControlService(db);
        var document = await service.CreateDocumentAsync(new CreateUnderwritingGuidelineDocumentRequest(
            ProgramName: "Old Program",
            CarrierId: null,
            LineOfBusiness: PolicyLineOfBusiness.InlandMarine,
            StateCode: "ALL",
            Title: "Old Guidelines",
            SourceFileName: "old.pdf",
            SourceBlobName: "guidelines/old.pdf",
            Notes: "Old notes"), Guid.NewGuid());
        var controls = await service.AddProposedControlsAsync(document.Value!.Id, new AddProposedUnderwritingControlsRequest([
            new CreateUnderwritingGuidelineControlRequest(
                UnderwritingControlItemType.DocumentChecklistItem,
                UnderwritingControlStage.Submission,
                UnderwritingControlSeverity.Warning,
                "loss-runs-required",
                "Loss runs",
                null,
                null,
                false,
                true,
                null,
                null,
                null,
                10)
        ]), Guid.NewGuid());

        var result = await service.UpdateDocumentAsync(document.Value.Id, new CreateUnderwritingGuidelineDocumentRequest(
            ProgramName: "Lloyds GL",
            CarrierId: carrier.Id,
            LineOfBusiness: PolicyLineOfBusiness.GeneralLiability,
            StateCode: "TX",
            Title: "Lloyds GL Guidelines",
            SourceFileName: "lloyds.pdf",
            SourceBlobName: "guidelines/lloyds.pdf",
            Notes: "Updated notes"), Guid.NewGuid());

        Assert.True(result.IsSuccess);
        Assert.Equal("Lloyds GL Guidelines", result.Value!.Title);
        Assert.Equal("Lloyds GL", result.Value.ProgramName);
        Assert.Equal(carrier.Id, result.Value.CarrierId);
        Assert.Equal(PolicyLineOfBusiness.GeneralLiability, result.Value.LineOfBusiness);
        Assert.Equal("TX", result.Value.StateCode);

        var control = await db.Set<UnderwritingGuidelineControl>().SingleAsync(c => c.Id == controls.Value!.Single().Id);
        Assert.Equal("Lloyds GL", control.ProgramName);
        Assert.Equal(carrier.Id, control.CarrierId);
        Assert.Equal(PolicyLineOfBusiness.GeneralLiability, control.LineOfBusiness);
        Assert.Equal("TX", control.StateCode);
        Assert.Contains(await db.Set<UnderwritingGuidelineAuditLog>().ToListAsync(), a => a.Action == "DocumentEdited");
    }

    [Fact]
    public async Task DeleteDocumentAsync_SoftDeletesDocumentAndDraftControls()
    {
        await using var db = CreateDb();
        var service = new UnderwritingGuidelineControlService(db);
        var document = await service.CreateDocumentAsync(new CreateUnderwritingGuidelineDocumentRequest(
            ProgramName: "Lloyds GL",
            CarrierId: null,
            LineOfBusiness: PolicyLineOfBusiness.GeneralLiability,
            StateCode: "ALL",
            Title: "Lloyds GL Guidelines",
            SourceFileName: null,
            SourceBlobName: null,
            Notes: null), Guid.NewGuid());
        await service.AddProposedControlsAsync(document.Value!.Id, new AddProposedUnderwritingControlsRequest([
            new CreateUnderwritingGuidelineControlRequest(
                UnderwritingControlItemType.DocumentChecklistItem,
                UnderwritingControlStage.Submission,
                UnderwritingControlSeverity.Warning,
                "loss-runs-required",
                "Loss runs",
                null,
                null,
                false,
                true,
                null,
                null,
                null,
                10)
        ]), Guid.NewGuid());

        var result = await service.DeleteDocumentAsync(document.Value.Id, Guid.NewGuid());

        Assert.True(result.IsSuccess);
        Assert.Empty(await service.GetDocumentsAsync());
        Assert.Empty(await service.GetControlsAsync(document.Value.Id));
        Assert.Contains(await db.Set<UnderwritingGuidelineAuditLog>().ToListAsync(), a => a.Action == "DocumentDeleted");
    }

    [Fact]
    public async Task DeleteDocumentAsync_BlocksPublishedControls()
    {
        await using var db = CreateDb();
        var service = new UnderwritingGuidelineControlService(db);
        var document = await service.CreateDocumentAsync(new CreateUnderwritingGuidelineDocumentRequest(
            ProgramName: "Lloyds GL",
            CarrierId: null,
            LineOfBusiness: PolicyLineOfBusiness.GeneralLiability,
            StateCode: "ALL",
            Title: "Lloyds GL Guidelines",
            SourceFileName: null,
            SourceBlobName: null,
            Notes: null), Guid.NewGuid());
        var controls = await service.AddProposedControlsAsync(document.Value!.Id, new AddProposedUnderwritingControlsRequest([
            new CreateUnderwritingGuidelineControlRequest(
                UnderwritingControlItemType.DocumentChecklistItem,
                UnderwritingControlStage.Submission,
                UnderwritingControlSeverity.Warning,
                "loss-runs-required",
                "Loss runs",
                null,
                null,
                false,
                true,
                null,
                null,
                null,
                10)
        ]), Guid.NewGuid());
        await service.ApproveControlAsync(controls.Value!.Single().Id, Guid.NewGuid(), null);
        await service.PublishControlAsync(controls.Value!.Single().Id, Guid.NewGuid(), null);

        var result = await service.DeleteDocumentAsync(document.Value.Id, Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.Equal("DOCUMENT_HAS_PUBLISHED_CONTROLS", result.ErrorCode);
        Assert.Single(await service.GetDocumentsAsync());
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }
}
