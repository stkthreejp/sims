using Microsoft.Extensions.Options;
using SIMS.Application.Configuration;
using SIMS.Application.Interfaces.Services;
using SIMS.Application.Services;
using SIMS.Domain.Entities;
using SIMS.Infrastructure.Data;
using SIMS.Infrastructure.Services;
using SIMS.Infrastructure.Workers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace SIMS.Infrastructure.Extensions;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // EF Core + PostgreSQL
        services.AddSingleton<AuditInterceptor>();
        services.AddDbContext<ApplicationDbContext>((sp, options) =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"),
                npgsql => npgsql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName))
                   .AddInterceptors(sp.GetRequiredService<AuditInterceptor>()));

        // Make the generic DbContext resolve to ApplicationDbContext for services that use it
        services.AddScoped<DbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        // ASP.NET Core Identity
        services.AddIdentity<User, Role>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequiredLength = 8;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.User.RequireUniqueEmail = true;
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

        // OIDC metadata — singleton so signing keys are fetched once and auto-refreshed
        services.AddSingleton<IConfigurationManager<OpenIdConnectConfiguration>>(sp =>
        {
            var cfg = sp.GetRequiredService<IConfiguration>();
            var tenantId = cfg["MicrosoftAuth:TenantId"]!;
            var metadataAddress = $"https://login.microsoftonline.com/{tenantId}/v2.0/.well-known/openid-configuration";
            return new ConfigurationManager<OpenIdConnectConfiguration>(
                metadataAddress, new OpenIdConnectConfigurationRetriever());
        });

        // Application services
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAgentService, AgentService>();
        services.AddScoped<ICarrierService, CarrierService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IInsuredService, InsuredService>();
        services.AddScoped<ISubmissionService, SubmissionService>();
        services.AddScoped<IQuoteService, QuoteService>();
        services.AddScoped<IPolicyService, PolicyService>();
        services.AddScoped<IRatingEngineService, RatingEngineService>();
        services.AddScoped<INoteService, NoteService>();
        services.AddSingleton<IBlobStorageService, AzureBlobStorageService>();
        services.AddScoped<IAttachmentService, AttachmentService>();
        services.AddScoped<IDocumentTemplateService, DocumentTemplateService>();
        services.AddScoped<IDocumentGenerationService, DocumentGenerationService>();
        services.AddScoped<IInboundEmailService, InboundEmailService>();
        services.AddScoped<IEmailIngestionService, EmailIngestionService>();
        services.AddScoped<IGeminiExtractionService, GeminiExtractionService>();
        services.AddScoped<ITaskTypeService, TaskTypeService>();
        services.AddScoped<IDueDateFormulaService, DueDateFormulaService>();
        services.AddScoped<IWorkflowEngineService, WorkflowEngineService>();
        services.AddScoped<ITaskInstanceService, TaskInstanceService>();
        services.AddScoped<ITaskNotificationService, TaskNotificationService>();
        services.AddScoped<IWorkflowTemplateService, WorkflowTemplateService>();
        services.AddScoped<ISystemEventService, SystemEventService>();
        services.AddScoped<IHolidayCalendarService, HolidayCalendarService>();
        services.AddScoped<IEscalationRuleService, EscalationRuleService>();
        services.AddScoped<IFeeCalculationService, FeeCalculationService>();
        services.AddScoped<IFeeAdminService, FeeAdminService>();
        services.AddScoped<ILedgerService, LedgerService>();
        services.AddScoped<ICarrierCommissionService, CarrierCommissionService>();
        services.AddScoped<IAgentCommissionService, AgentCommissionService>();
        services.AddScoped<IInvoicingService, InvoicingService>();
        services.AddScoped<IReceiptsService, ReceiptsService>();
        services.AddScoped<ICashApplicationService, CashApplicationService>();
        services.AddScoped<ICashDistributionService, CashDistributionService>();
        services.AddScoped<IDisbursementService, DisbursementService>();
        // QBO
        services.Configure<QboSettings>(configuration.GetSection("Qbo"));
        services.AddScoped<IQboTokenService, QboTokenService>();
        services.AddScoped<IQboApiClient, QboApiClient>();
        services.AddScoped<IJournalDriver, CsvJournalDriver>();
        services.AddScoped<IJournalDriver, QboJournalDriver>();
        services.AddScoped<IRollupService, RollupService>();
        services.AddScoped<IPeriodCloseService, PeriodCloseService>();
        services.AddScoped<IVoidService, VoidService>();
        services.AddScoped<IActivityService, ActivityService>();
        services.AddScoped<IPayeeStatementService, PayeeStatementService>();
        services.AddScoped<IWireSheetPdfService, WireSheetPdfService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<ICarrierRatingAssignmentService, CarrierRatingAssignmentService>();
        services.AddScoped<IShadowRatingService, ShadowRatingService>();
        services.AddScoped<IUWWriteupService, UWWriteupService>();
        services.AddHttpClient("gemini");
        services.AddHttpClient("qbo_oauth");
        services.AddHttpClient("qbo_api");
        services.AddHostedService<EmailIngestionWorker>();
        services.AddHostedService<TaskNotificationWorker>();
        services.AddHostedService<TaskEscalationWorker>();
        services.AddHostedService<QboSyncRetryWorker>();
        services.AddHostedService<ShadowRateDailyReportWorker>();

        return services;
    }

    public static async Task SeedDatabaseAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<Role>>();

        await db.Database.MigrateAsync();

        // Seed permissions
        var permissions = new[]
        {
            ("insureds.view", "View Insureds", "Insureds"),
            ("insureds.create", "Create Insureds", "Insureds"),
            ("insureds.edit", "Edit Insureds", "Insureds"),
            ("insureds.delete", "Delete Insureds", "Insureds"),
            ("policies.view", "View Policies", "Policies"),
            ("policies.create", "Create Policies", "Policies"),
            ("policies.edit", "Edit Policies", "Policies"),
            ("policies.delete", "Delete Policies", "Policies"),
            ("policies.bind", "Bind Policies", "Policies"),
            ("policies.issue", "Issue Policies", "Policies"),
            ("policies.endorse", "Endorse Policies", "Policies"),
            ("policies.renew", "Renew Policies", "Policies"),
            ("policies.cancel", "Cancel Policies", "Policies"),
            ("policies.notes.create", "Create Notes", "Notes"),
            ("policies.notes.edit", "Edit Notes", "Notes"),
            ("policies.notes.delete", "Delete Notes", "Notes"),
            ("policies.attachments.upload", "Upload Attachments", "Attachments"),
            ("policies.attachments.delete", "Delete Attachments", "Attachments"),
            ("admin.users.view", "View Users", "Admin"),
            ("admin.users.manage", "Manage Users", "Admin"),
            ("admin.roles.view", "View Roles", "Admin"),
            ("admin.roles.manage", "Manage Roles", "Admin"),
            // Navigation section permissions — control sidebar visibility
            ("nav.submissions", "Submissions Section", "Navigation"),
            ("nav.inbox", "Inbox Section", "Navigation"),
            ("nav.agents", "Agents Section", "Navigation"),
            ("nav.carriers", "Carriers Section", "Navigation"),
            ("nav.document-library", "Document Library Section", "Navigation"),
            ("nav.reports", "Reports Section", "Navigation"),
            ("nav.billing", "Accounting / Billing Section", "Navigation"),
            ("nav.admin.rating", "Rating Engine Admin", "Navigation"),
            ("nav.admin.tasks", "Task Engine Admin", "Navigation"),
            ("nav.admin.fees", "Fee Rules Admin", "Navigation"),
        };

        foreach (var (name, display, category) in permissions)
        {
            if (!db.Permissions.Any(p => p.Name == name))
                db.Permissions.Add(new Permission { Name = name, DisplayName = display, Category = category });
        }
        await db.SaveChangesAsync();

        // Seed roles
        var roleDefinitions = new Dictionary<string, (string description, string[] permissions)>
        {
            ["Admin"] = ("Full system access", permissions.Select(p => p.Item1).ToArray()),
            ["Underwriter"] = ("Underwriting staff", new[] {
                "insureds.view", "insureds.create", "insureds.edit",
                "policies.view", "policies.create", "policies.edit", "policies.bind", "policies.issue",
                "policies.endorse", "policies.renew", "policies.cancel",
                "policies.notes.create", "policies.notes.edit", "policies.notes.delete",
                "policies.attachments.upload", "policies.attachments.delete",
                "nav.submissions", "nav.inbox", "nav.reports",
            }),
            ["CSR"] = ("Customer service", new[] {
                "insureds.view", "insureds.create", "insureds.edit",
                "policies.view", "policies.notes.create", "policies.notes.edit",
                "policies.attachments.upload",
                "nav.submissions",
            }),
            ["ReadOnly"] = ("Read only access", new[] {
                "insureds.view", "policies.view",
            }),
        };

        // Create any missing roles and assign their full permission set
        foreach (var (roleName, (description, perms)) in roleDefinitions)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var role = new Role { Name = roleName, Description = description, IsSystemRole = true };
                await roleManager.CreateAsync(role);
            }

            var existingRole = await roleManager.FindByNameAsync(roleName);
            if (existingRole == null) continue;

            // Add any permissions not yet assigned to this role (idempotent)
            var existingPermIds = db.RolePermissions
                .Where(rp => rp.RoleId == existingRole.Id)
                .Select(rp => rp.PermissionId)
                .ToHashSet();

            var permEntities = db.Permissions
                .Where(p => perms.Contains(p.Name) && !existingPermIds.Contains(p.Id))
                .ToList();

            foreach (var perm in permEntities)
                db.RolePermissions.Add(new RolePermission { RoleId = existingRole.Id, PermissionId = perm.Id });
        }
        await db.SaveChangesAsync();

        // Seed admin user
        if (await userManager.FindByNameAsync("admin") == null)
        {
            var admin = new User
            {
                UserName = "admin",
                Email = "admin@SIMS.local",
                FirstName = "System",
                LastName = "Admin",
                EmailConfirmed = true,
                MustChangePassword = true
            };
            await userManager.CreateAsync(admin, "Admin@123!");
            await userManager.AddToRoleAsync(admin, "Admin");
        }
    }
}
