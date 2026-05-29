using Microsoft.AspNetCore.Authorization;
using SIMS.API.Controllers;
using SIMS.API.Controllers.Admin;
using SIMS.Application.Security;
using Xunit;

namespace SIMS.Application.Tests.Controllers;

public class AuthHardeningControllerTests
{
    [Theory]
    [InlineData(typeof(QuotesController), nameof(QuotesController.Create), AppPermissions.PoliciesCreate)]
    [InlineData(typeof(QuotesController), nameof(QuotesController.Update), AppPermissions.PoliciesEdit)]
    [InlineData(typeof(QuotesController), nameof(QuotesController.Rate), AppPermissions.PoliciesEdit)]
    [InlineData(typeof(QuotesController), nameof(QuotesController.Bind), AppPermissions.PoliciesBind)]
    [InlineData(typeof(QuotesController), nameof(QuotesController.Delete), AppPermissions.PoliciesDelete)]
    [InlineData(typeof(SubmissionsController), nameof(SubmissionsController.Create), AppPermissions.UnderwritingManage)]
    [InlineData(typeof(SubmissionsController), nameof(SubmissionsController.Update), AppPermissions.UnderwritingManage)]
    [InlineData(typeof(SubmissionsController), nameof(SubmissionsController.SetLinesOfBusiness), AppPermissions.UnderwritingManage)]
    [InlineData(typeof(SubmissionsController), nameof(SubmissionsController.Delete), AppPermissions.UnderwritingManage)]
    [InlineData(typeof(UsersController), nameof(UsersController.GetAll), AppPermissions.AdminUsersView)]
    [InlineData(typeof(UsersController), nameof(UsersController.GetById), AppPermissions.AdminUsersView)]
    [InlineData(typeof(InsuredsController), nameof(InsuredsController.Create), AppPermissions.InsuredsCreate)]
    [InlineData(typeof(InsuredsController), nameof(InsuredsController.Update), AppPermissions.InsuredsEdit)]
    [InlineData(typeof(InsuredsController), nameof(InsuredsController.Delete), AppPermissions.InsuredsDelete)]
    public void WorkflowEndpoints_RequireExpectedPermission(Type controllerType, string methodName, string expectedPolicy)
    {
        Assert.Equal(expectedPolicy, GetMethodPolicy(controllerType, methodName));
    }

    [Theory]
    [InlineData(typeof(AgentsController), nameof(AgentsController.Create))]
    [InlineData(typeof(AgentsController), nameof(AgentsController.Update))]
    [InlineData(typeof(AgentsController), nameof(AgentsController.Delete))]
    [InlineData(typeof(AgentsController), nameof(AgentsController.AddLocation))]
    [InlineData(typeof(AgentsController), nameof(AgentsController.UpdateLocation))]
    [InlineData(typeof(AgentsController), nameof(AgentsController.DeleteLocation))]
    [InlineData(typeof(AgentsController), nameof(AgentsController.AddContact))]
    [InlineData(typeof(AgentsController), nameof(AgentsController.UpdateContact))]
    [InlineData(typeof(AgentsController), nameof(AgentsController.DeleteContact))]
    [InlineData(typeof(CarriersController), nameof(CarriersController.Create))]
    [InlineData(typeof(CarriersController), nameof(CarriersController.Update))]
    [InlineData(typeof(CarriersController), nameof(CarriersController.Delete))]
    [InlineData(typeof(CarriersController), nameof(CarriersController.AddContact))]
    [InlineData(typeof(CarriersController), nameof(CarriersController.UpdateContact))]
    [InlineData(typeof(CarriersController), nameof(CarriersController.DeleteContact))]
    public void PartyMaintenanceEndpoints_RequireSystemAdmin(Type controllerType, string methodName)
    {
        Assert.Equal(AppPermissions.AdminSystemManage, GetMethodPolicy(controllerType, methodName));
    }

    [Theory]
    [InlineData(nameof(AttachmentsController.GetSubmission))]
    [InlineData(nameof(AttachmentsController.GetQuote))]
    [InlineData(nameof(AttachmentsController.GetCarrier))]
    [InlineData(nameof(AttachmentsController.GetAgent))]
    [InlineData(nameof(AttachmentsController.GetInsured))]
    [InlineData(nameof(AttachmentsController.GetDownloadUrl))]
    public void AttachmentReadEndpoints_RequirePolicyView(string methodName)
    {
        Assert.Equal(AppPermissions.PoliciesView, GetMethodPolicy(typeof(AttachmentsController), methodName));
    }

    [Theory]
    [InlineData(nameof(AttachmentsController.UploadSubmission))]
    [InlineData(nameof(AttachmentsController.UploadQuote))]
    [InlineData(nameof(AttachmentsController.UploadCarrier))]
    [InlineData(nameof(AttachmentsController.UploadAgent))]
    [InlineData(nameof(AttachmentsController.UploadInsured))]
    public void AttachmentUploadEndpoints_RequireAttachmentUploadPermission(string methodName)
    {
        Assert.Equal(AppPermissions.AttachmentsUpload, GetMethodPolicy(typeof(AttachmentsController), methodName));
    }

    [Fact]
    public void AttachmentDeleteEndpoint_RequiresAttachmentDeletePermission()
    {
        Assert.Equal(AppPermissions.AttachmentsDelete, GetMethodPolicy(typeof(AttachmentsController), nameof(AttachmentsController.Delete)));
    }

    [Theory]
    [InlineData(nameof(PoliciesController.GetAttachments), AppPermissions.PoliciesView)]
    [InlineData(nameof(PoliciesController.UploadAttachment), AppPermissions.AttachmentsUpload)]
    [InlineData(nameof(PoliciesController.DownloadAttachment), AppPermissions.PoliciesView)]
    [InlineData(nameof(PoliciesController.DeleteAttachment), AppPermissions.AttachmentsDelete)]
    public void LegacyPolicyAttachmentEndpoints_RequireExpectedPermission(string methodName, string expectedPolicy)
    {
        Assert.Equal(expectedPolicy, GetMethodPolicy(typeof(PoliciesController), methodName));
    }

    [Theory]
    [InlineData(typeof(NotesController), nameof(NotesController.GetAll), AppPermissions.PoliciesView)]
    [InlineData(typeof(NotesController), nameof(NotesController.GetById), AppPermissions.PoliciesView)]
    [InlineData(typeof(NotesController), nameof(NotesController.Create), AppPermissions.NotesCreate)]
    [InlineData(typeof(NotesController), nameof(NotesController.Update), AppPermissions.NotesEdit)]
    [InlineData(typeof(NotesController), nameof(NotesController.TogglePin), AppPermissions.NotesEdit)]
    [InlineData(typeof(NotesController), nameof(NotesController.Delete), AppPermissions.NotesDelete)]
    [InlineData(typeof(PoliciesController), nameof(PoliciesController.GetNotes), AppPermissions.PoliciesView)]
    [InlineData(typeof(PoliciesController), nameof(PoliciesController.CreateNote), AppPermissions.NotesCreate)]
    [InlineData(typeof(PoliciesController), nameof(PoliciesController.UpdateNote), AppPermissions.NotesEdit)]
    [InlineData(typeof(PoliciesController), nameof(PoliciesController.TogglePinNote), AppPermissions.NotesEdit)]
    [InlineData(typeof(PoliciesController), nameof(PoliciesController.DeleteNote), AppPermissions.NotesDelete)]
    public void NoteEndpoints_RequireExpectedPermission(Type controllerType, string methodName, string expectedPolicy)
    {
        Assert.Equal(expectedPolicy, GetMethodPolicy(controllerType, methodName));
    }

    [Theory]
    [InlineData(nameof(LegalRequirementsController.CreateSource))]
    [InlineData(nameof(LegalRequirementsController.UpdateSource))]
    [InlineData(nameof(LegalRequirementsController.ScanSource))]
    [InlineData(nameof(LegalRequirementsController.ImportOden))]
    [InlineData(nameof(LegalRequirementsController.SimulateChange))]
    [InlineData(nameof(LegalRequirementsController.ApproveScanResult))]
    [InlineData(nameof(LegalRequirementsController.RejectScanResult))]
    public void LegalRequirementMutationEndpoints_RequireSystemAdmin(string methodName)
    {
        Assert.Equal(AppPermissions.AdminSystemManage, GetMethodPolicy(typeof(LegalRequirementsController), methodName));
    }

    [Fact]
    public void IntermediariesController_RequiresAdminSystemManagePolicy()
    {
        Assert.Equal(AppPermissions.AdminSystemManage, GetClassPolicy(typeof(IntermediariesController)));
    }

    private static string? GetClassPolicy(Type controllerType) =>
        controllerType
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .SingleOrDefault()
            ?.Policy;

    private static string? GetMethodPolicy(Type controllerType, string methodName)
    {
        var methods = controllerType.GetMethods().Where(method => method.Name == methodName).ToArray();
        Assert.Single(methods);

        return methods[0]
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .SingleOrDefault()
            ?.Policy;
    }
}
