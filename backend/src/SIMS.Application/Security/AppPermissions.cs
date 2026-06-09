namespace SIMS.Application.Security;

public static class AppPermissions
{
    public const string InsuredsView = "insureds.view";
    public const string InsuredsCreate = "insureds.create";
    public const string InsuredsEdit = "insureds.edit";
    public const string InsuredsDelete = "insureds.delete";

    public const string PoliciesView = "policies.view";
    public const string PoliciesCreate = "policies.create";
    public const string PoliciesEdit = "policies.edit";
    public const string PoliciesDelete = "policies.delete";
    public const string PoliciesBind = "policies.bind";
    public const string PoliciesIssue = "policies.issue";
    public const string PoliciesEndorse = "policies.endorse";
    public const string PoliciesRenew = "policies.renew";
    public const string PoliciesCancel = "policies.cancel";
    public const string PoliciesVoidTestBind = "policies.void_test_bind";

    public const string NotesCreate = "policies.notes.create";
    public const string NotesEdit = "policies.notes.edit";
    public const string NotesDelete = "policies.notes.delete";

    public const string AttachmentsUpload = "policies.attachments.upload";
    public const string AttachmentsDelete = "policies.attachments.delete";

    public const string AdminUsersView = "admin.users.view";
    public const string AdminUsersManage = "admin.users.manage";
    public const string AdminRolesView = "admin.roles.view";
    public const string AdminRolesManage = "admin.roles.manage";
    public const string AdminSystemManage = "admin.system.manage";
    public const string AdminUnderwritingControlsManage = "admin.underwriting-controls.manage";
    public const string AdminUnderwritingControlsPublish = "admin.underwriting-controls.publish";

    public const string UnderwritingManage = "underwriting.manage";
    public const string UnderwritingClearanceOverride = "underwriting.clearance.override";
    public const string UnderwritingAuthorityApprove = "underwriting.authority.approve";
    public const string AccountingManage = "accounting.manage";
    public const string AccountingAdmin = "accounting.admin";
    public const string RatingManage = "rating.manage";
    public const string RatingAdmin = "rating.admin";
    public const string ReportsView = "reports.view";

    public const string NavSubmissions = "nav.submissions";
    public const string NavInbox = "nav.inbox";
    public const string NavAgents = "nav.agents";
    public const string NavCarriers = "nav.carriers";
    public const string NavDocumentLibrary = "nav.document-library";
    public const string NavComplianceDocumentation = "nav.compliance-documentation";
    public const string NavReports = "nav.reports";
    public const string NavBilling = "nav.billing";
    public const string NavAdminRating = "nav.admin.rating";
    public const string NavAdminTasks = "nav.admin.tasks";
    public const string NavAdminFees = "nav.admin.fees";

    public static readonly PermissionDefinition[] All =
    [
        new(InsuredsView, "View Insureds", "Insureds"),
        new(InsuredsCreate, "Create Insureds", "Insureds"),
        new(InsuredsEdit, "Edit Insureds", "Insureds"),
        new(InsuredsDelete, "Delete Insureds", "Insureds"),

        new(PoliciesView, "View Policies", "Policies"),
        new(PoliciesCreate, "Create Policies", "Policies"),
        new(PoliciesEdit, "Edit Policies", "Policies"),
        new(PoliciesDelete, "Delete Policies", "Policies"),
        new(PoliciesBind, "Bind Policies", "Policies"),
        new(PoliciesIssue, "Issue Policies", "Policies"),
        new(PoliciesEndorse, "Endorse Policies", "Policies"),
        new(PoliciesRenew, "Renew Policies", "Policies"),
        new(PoliciesCancel, "Cancel Policies", "Policies"),
        new(PoliciesVoidTestBind, "Void Test Binds", "Policies"),

        new(NotesCreate, "Create Notes", "Notes"),
        new(NotesEdit, "Edit Notes", "Notes"),
        new(NotesDelete, "Delete Notes", "Notes"),

        new(AttachmentsUpload, "Upload Attachments", "Attachments"),
        new(AttachmentsDelete, "Delete Attachments", "Attachments"),

        new(AdminUsersView, "View Users", "Admin"),
        new(AdminUsersManage, "Manage Users", "Admin"),
        new(AdminRolesView, "View Roles", "Admin"),
        new(AdminRolesManage, "Manage Roles", "Admin"),
        new(AdminSystemManage, "Manage System Administration", "Admin"),
        new(AdminUnderwritingControlsManage, "Manage Underwriting Control Setup", "Admin"),
        new(AdminUnderwritingControlsPublish, "Publish Underwriting Controls", "Admin"),

        new(UnderwritingManage, "Manage Underwriting Workflows", "Underwriting"),
        new(UnderwritingClearanceOverride, "Override Underwriting Clearance Blocks", "Underwriting"),
        new(UnderwritingAuthorityApprove, "Approve Underwriting Authority Exceptions", "Underwriting"),
        new(AccountingManage, "Manage Accounting Workflows", "Accounting"),
        new(AccountingAdmin, "Administer Accounting Workflows", "Accounting"),
        new(RatingManage, "Manage Rating Workflows", "Rating"),
        new(RatingAdmin, "Administer Rating Workflows", "Rating"),
        new(ReportsView, "View Reports", "Reports"),

        new(NavSubmissions, "Submissions Section", "Navigation"),
        new(NavInbox, "Inbox Section", "Navigation"),
        new(NavAgents, "Agents Section", "Navigation"),
        new(NavCarriers, "Carriers Section", "Navigation"),
        new(NavDocumentLibrary, "Document Library Section", "Navigation"),
        new(NavComplianceDocumentation, "Compliance Documentation Section", "Navigation"),
        new(NavReports, "Reports Section", "Navigation"),
        new(NavBilling, "Accounting / Billing Section", "Navigation"),
        new(NavAdminRating, "Rating Engine Admin", "Navigation"),
        new(NavAdminTasks, "Task Engine Admin", "Navigation"),
        new(NavAdminFees, "Fee Rules Admin", "Navigation"),
    ];
}

public sealed record PermissionDefinition(string Name, string DisplayName, string Category);
