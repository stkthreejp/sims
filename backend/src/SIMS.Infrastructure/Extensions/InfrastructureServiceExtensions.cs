using Microsoft.Extensions.Options;
using SIMS.Application.Configuration;
using SIMS.Application.Interfaces.Services;
using SIMS.Application.Security;
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
using System.Text.Json;

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
        var safetyAnalyticsConnection = configuration.GetConnectionString("SafetyAnalyticsConnection");
        if (!string.IsNullOrWhiteSpace(safetyAnalyticsConnection))
        {
            services.AddDbContext<SafetyAnalyticsDbContext>(options =>
                options.UseNpgsql(safetyAnalyticsConnection,
                    npgsql => npgsql.MigrationsAssembly(typeof(SafetyAnalyticsDbContext).Assembly.FullName)));
        }

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
        services.AddScoped<IGeocodingService, GoogleGeocodingService>();
        services.AddScoped<ISubmissionService, SubmissionService>();
        services.AddScoped<IQuoteChecklistService, QuoteChecklistService>();
        services.AddScoped<IUnderwritingClearanceService, UnderwritingClearanceService>();
        services.AddScoped<IPolicyNumberService, PolicyNumberService>();
        services.AddScoped<IPolicyNumberAdminService, PolicyNumberAdminService>();
        services.AddScoped<IPolicyTransactionLifecycleService, PolicyTransactionLifecycleService>();
        services.AddScoped<IPolicyVersionService, PolicyVersionService>();
        services.AddScoped<IQuoteService, QuoteService>();
        services.AddScoped<IPolicyService, PolicyService>();
        services.AddScoped<IRatingEngineService, RatingEngineService>();
        services.AddScoped<INoteService, NoteService>();
        services.AddSingleton<IBlobStorageService, AzureBlobStorageService>();
        if (string.Equals(configuration["Uploads:MalwareScanning:Provider"], "ClamAV", StringComparison.OrdinalIgnoreCase))
            services.AddScoped<IFileScanService, ClamAvFileScanService>();
        else
            services.AddScoped<IFileScanService, NoOpFileScanService>();
        services.AddScoped<IAttachmentService, AttachmentService>();
        services.AddScoped<IDocumentTemplateService, DocumentTemplateService>();
        services.AddScoped<IDocumentGenerationService, DocumentGenerationService>();
        services.AddScoped<IDocumentMergeService, DocumentMergeService>();
        services.AddScoped<IOutboundEmailSenderService, GraphOutboundEmailSenderService>();
        services.AddScoped<IPolicyFormService, PolicyFormService>();
        services.AddScoped<IQuotePolicyFormSelectionService, QuotePolicyFormSelectionService>();
        services.AddScoped<IPolicyAssemblyService, PolicyAssemblyService>();
        services.AddScoped<IProposalGenerationService, ProposalGenerationService>();
        services.AddScoped<IHtmlToPdfService, SyncfusionHtmlToPdfService>();
        services.AddScoped<IOutboundCommunicationService, OutboundCommunicationService>();
        services.AddScoped<IComplianceDocumentService, ComplianceDocumentService>();
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
        services.Configure<FmcsaSocrataSettings>(configuration.GetSection("Fmcsa:Socrata"));
        services.Configure<FmcsaJobSettings>(configuration.GetSection("Fmcsa:Jobs"));
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
        services.AddScoped<ILegiScanService, LegiScanService>();
        services.AddScoped<IFmcsaSafetyService, FmcsaSafetyService>();
        services.AddScoped<IAutoSafetyReportService, AutoSafetyReportService>();
        services.AddScoped<IFmcsaSafetyAnalyticsService, FmcsaSafetyAnalyticsService>();
        services.AddScoped<IFmcsaInspectionEnrichmentService, FmcsaInspectionEnrichmentService>();
        services.AddScoped<FmcsaSocrataClient>();
        services.Configure<LegiScanSettings>(configuration.GetSection("LegiScan"));
        services.AddScoped<LegiScanClient>();
        services.AddHttpClient("gemini", c => c.Timeout = TimeSpan.FromSeconds(
            int.TryParse(configuration["HttpClients:GeminiTimeoutSeconds"], out var geminiTimeout) ? geminiTimeout : 60));
        services.AddHttpClient("qbo_oauth", c => c.Timeout = TimeSpan.FromSeconds(
            int.TryParse(configuration["HttpClients:QboOAuthTimeoutSeconds"], out var qboOAuthTimeout) ? qboOAuthTimeout : 30));
        services.AddHttpClient("qbo_api", c => c.Timeout = TimeSpan.FromSeconds(
            int.TryParse(configuration["HttpClients:QboApiTimeoutSeconds"], out var qboApiTimeout) ? qboApiTimeout : 30));
        services.AddHttpClient("fmcsa_socrata", c =>
        {
            c.BaseAddress = new Uri(configuration["Fmcsa:Socrata:BaseUrl"] ?? "https://data.transportation.gov");
            c.Timeout = TimeSpan.FromSeconds(
                int.TryParse(configuration["HttpClients:FmcsaSocrataTimeoutSeconds"], out var fmcsaTimeout) ? fmcsaTimeout : 60);
        });
        services.AddHttpClient("fmcsa_qcmobile", c =>
        {
            c.BaseAddress = new Uri(configuration["Fmcsa:Socrata:QcMobileBaseUrl"] ?? "https://mobile.fmcsa.dot.gov");
            c.Timeout = TimeSpan.FromSeconds(
                int.TryParse(configuration["HttpClients:FmcsaQCMobileTimeoutSeconds"], out var fmcsaTimeout) ? fmcsaTimeout : 30);
        });
        services.AddHttpClient("nhtsa_vpic", c =>
        {
            c.BaseAddress = new Uri("https://vpic.nhtsa.dot.gov");
            c.Timeout = TimeSpan.FromSeconds(15);
        });
        services.AddHttpClient("google_geocoding", c =>
        {
            c.BaseAddress = new Uri("https://maps.googleapis.com");
            c.Timeout = TimeSpan.FromSeconds(
                int.TryParse(configuration["HttpClients:GoogleGeocodingTimeoutSeconds"], out var googleTimeout) ? googleTimeout : 10);
        });
        services.AddHttpClient("legiscan", c =>
        {
            c.BaseAddress = new Uri(configuration["LegiScan:BaseUrl"] ?? "https://api.legiscan.com");
            c.Timeout = TimeSpan.FromSeconds(
                int.TryParse(configuration["HttpClients:LegiScanTimeoutSeconds"], out var legiscanTimeout) ? legiscanTimeout : 30);
        });
        services.AddHostedService<EmailIngestionWorker>();
        services.AddHostedService<TaskNotificationWorker>();
        services.AddHostedService<TaskEscalationWorker>();
        services.AddHostedService<QboSyncRetryWorker>();
        services.AddHostedService<ShadowRateDailyReportWorker>();
        services.AddHostedService<FmcsaScheduledJobsWorker>();

        return services;
    }

    public static async Task SeedDatabaseAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<Role>>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        await db.Database.MigrateAsync();

        // Seed permissions
        foreach (var permission in AppPermissions.All)
        {
            if (!db.Permissions.Any(p => p.Name == permission.Name))
                db.Permissions.Add(new Permission
                {
                    Name = permission.Name,
                    DisplayName = permission.DisplayName,
                    Category = permission.Category
                });
        }
        await db.SaveChangesAsync();

        // Seed roles
        var roleDefinitions = new Dictionary<string, (string description, string[] permissions)>
        {
            ["Admin"] = ("Full system access", AppPermissions.All.Select(p => p.Name).ToArray()),
            ["Underwriter"] = ("Underwriting staff", new[] {
                AppPermissions.InsuredsView, AppPermissions.InsuredsCreate, AppPermissions.InsuredsEdit,
                AppPermissions.PoliciesView, AppPermissions.PoliciesCreate, AppPermissions.PoliciesEdit,
                AppPermissions.PoliciesBind, AppPermissions.PoliciesIssue, AppPermissions.PoliciesEndorse,
                AppPermissions.PoliciesRenew, AppPermissions.PoliciesCancel,
                AppPermissions.NotesCreate, AppPermissions.NotesEdit, AppPermissions.NotesDelete,
                AppPermissions.AttachmentsUpload, AppPermissions.AttachmentsDelete,
                AppPermissions.UnderwritingManage, AppPermissions.AccountingManage,
                AppPermissions.RatingManage, AppPermissions.ReportsView,
                AppPermissions.NavSubmissions, AppPermissions.NavInbox, AppPermissions.NavReports,
                AppPermissions.NavComplianceDocumentation,
            }),
            ["CSR"] = ("Customer service", new[] {
                AppPermissions.InsuredsView, AppPermissions.InsuredsCreate, AppPermissions.InsuredsEdit,
                AppPermissions.PoliciesView, AppPermissions.NotesCreate, AppPermissions.NotesEdit,
                AppPermissions.AttachmentsUpload,
                AppPermissions.NavSubmissions,
            }),
            ["ReadOnly"] = ("Read only access", new[] {
                AppPermissions.InsuredsView, AppPermissions.PoliciesView,
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

        await SeedLegalRequirementSectionsAsync(db);
        await SeedLegalTrackedSourcesAsync(db);
        await SeedComplianceDocumentsAsync(db);

        // Optional first-admin bootstrap. Never seed a hard-coded password.
        var adminUserName = configuration["AdminBootstrap:UserName"] ?? "admin";
        var adminPassword = configuration["AdminBootstrap:Password"];
        if (await userManager.FindByNameAsync(adminUserName) == null && !string.IsNullOrWhiteSpace(adminPassword))
        {
            var admin = new User
            {
                UserName = adminUserName,
                Email = configuration["AdminBootstrap:Email"] ?? "admin@SIMS.local",
                FirstName = configuration["AdminBootstrap:FirstName"] ?? "System",
                LastName = configuration["AdminBootstrap:LastName"] ?? "Admin",
                EmailConfirmed = true,
                MustChangePassword = true
            };
            var createResult = await userManager.CreateAsync(admin, adminPassword);
            if (!createResult.Succeeded)
                throw new InvalidOperationException("Admin bootstrap failed: " +
                    string.Join(", ", createResult.Errors.Select(e => e.Description)));

            var roleResult = await userManager.AddToRoleAsync(admin, "Admin");
            if (!roleResult.Succeeded)
                throw new InvalidOperationException("Admin bootstrap role assignment failed: " +
                    string.Join(", ", roleResult.Errors.Select(e => e.Description)));
        }
    }

    private static async Task SeedLegalRequirementSectionsAsync(ApplicationDbContext db)
    {
        var seedDir = Path.Combine(AppContext.BaseDirectory, "Data", "Seeds");
        if (!Directory.Exists(seedDir))
            return;

        var seedRows = new List<LegalRequirementSeedRow>();
        foreach (var seedPath in Directory.EnumerateFiles(seedDir, "oden-commercial-*.json"))
        {
            var json = await File.ReadAllTextAsync(seedPath);
            seedRows.AddRange(JsonSerializer.Deserialize<List<LegalRequirementSeedRow>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? []);
        }

        if (seedRows.Count == 0)
            return;

        var existingKeys = await db.LegalRequirementSections
            .Select(r => new { r.State, r.LineOfBusiness, r.Action, r.Category, r.Topic })
            .ToListAsync();
        var existing = existingKeys
            .Select(r => $"{r.State}|{r.LineOfBusiness}|{r.Action}|{r.Category}|{r.Topic}")
            .ToHashSet();

        foreach (var row in seedRows)
        {
            var key = $"{row.State}|{row.LineOfBusiness}|{row.Action}|{row.Category}|{row.Topic}";
            if (existing.Contains(key))
                continue;

            db.LegalRequirementSections.Add(new LegalRequirementSection
            {
                State = row.State,
                LineOfBusiness = row.LineOfBusiness,
                Action = row.Action,
                Category = row.Category,
                Topic = TrimToMaxLength(row.Topic, 160),
                RequirementText = row.RequirementText,
                Citations = row.Citations,
                SourceName = row.SourceName,
                SourceDocument = row.SourceDocument,
                SourceCreatedAt = row.SourceCreatedAt,
                ReviewStatus = row.ReviewStatus,
                LastVerifiedAt = DateTime.UtcNow,
                SortOrder = row.SortOrder
            });
            existing.Add(key);
        }

        await db.SaveChangesAsync();
    }

    private sealed record LegalRequirementSeedRow(
        string State,
        string LineOfBusiness,
        string Action,
        string Category,
        string Topic,
        string RequirementText,
        string[] Citations,
        string SourceName,
        string SourceDocument,
        DateTime SourceCreatedAt,
        string ReviewStatus,
        int SortOrder);

    private static string TrimToMaxLength(string value, int maxLength)
    {
        if (value.Length <= maxLength)
            return value;

        return value[..maxLength];
    }

    private static async Task SeedLegalTrackedSourcesAsync(ApplicationDbContext db)
    {
        var sources = new[]
        {
            new LegalTrackedSource
            {
                State = "All",
                Name = "Oden Online Cancellation Chart",
                SourceType = "Oden Export",
                ScanCadence = "Manual",
                Notes = "Initial source of truth. Upload the latest Oden HTML export from the Source Scans tab."
            },
            new LegalTrackedSource
            {
                State = "All",
                Name = "Oden Online Nonrenewal Chart",
                SourceType = "Oden Export",
                ScanCadence = "Manual",
                Notes = "Initial nonrenewal source of truth. Upload the latest Oden HTML export from the Source Scans tab."
            },
            new LegalTrackedSource
            {
                State = "Alabama",
                Name = "Alabama DOI Bulletins",
                SourceType = "DOI Bulletin",
                ScanCadence = "Monthly",
                Notes = "Placeholder for state DOI bulletin monitoring."
            },
            new LegalTrackedSource
            {
                State = "Florida",
                Name = "Florida Cancellation Statutes and Rules",
                SourceType = "Statute/Regulation",
                ScanCadence = "Monthly",
                Notes = "Placeholder for statute and regulation monitoring."
            },
            new LegalTrackedSource
            {
                State = "Texas",
                Name = "Texas Cancellation Statutes and Rules",
                SourceType = "Statute/Regulation",
                ScanCadence = "Monthly",
                Notes = "Placeholder for statute and regulation monitoring."
            },
            new LegalTrackedSource
            {
                State = "North Carolina",
                Name = "North Carolina Cancellation Statutes and Rules",
                SourceType = "Statute/Regulation",
                ScanCadence = "Monthly",
                Notes = "Placeholder for statute and regulation monitoring."
            }
        };

        foreach (var source in sources)
        {
            var exists = await db.LegalTrackedSources.AnyAsync(s =>
                s.State == source.State &&
                s.Name == source.Name &&
                s.SourceType == source.SourceType);

            if (!exists)
                db.LegalTrackedSources.Add(source);
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedComplianceDocumentsAsync(ApplicationDbContext db)
    {
        var documents = new[]
        {
            ("IT Data Security Policy", "Security", "Policy", "Annual", new[] { "IT", "Data", "Security" }),
            ("Business Continuity Plan", "Business Continuity", "Plan", "Annual", new[] { "BCP", "Operations" }),
            ("Disaster Recovery Plan", "Business Continuity", "Plan", "Annual", new[] { "DR", "IT" }),
            ("Incident Response Plan", "Security", "Plan", "Annual", new[] { "Security", "Incident Response" }),
            ("Access Control Policy", "Security", "Policy", "Annual", new[] { "Access", "Identity" }),
            ("Acceptable Use Policy", "IT", "Policy", "Annual", new[] { "IT", "Employees" }),
            ("Vendor Management Policy", "Vendor Management", "Policy", "Annual", new[] { "Vendors", "Third Party" }),
            ("Data Retention Policy", "Privacy", "Policy", "Annual", new[] { "Data", "Records" }),
            ("Privacy Policy", "Privacy", "Policy", "Annual", new[] { "Privacy", "Data" }),
            ("Change Management Procedure", "Operations", "Procedure", "Annual", new[] { "Change Management", "IT" }),
            ("Backup and Recovery Procedure", "IT", "Procedure", "Annual", new[] { "Backup", "Recovery" }),
            ("Security Awareness Training Procedure", "Security", "Procedure", "Annual", new[] { "Training", "Employees" })
        };

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        foreach (var (title, category, type, cadence, tags) in documents)
        {
            var exists = await db.ComplianceDocuments.AnyAsync(d => d.Title == title);
            if (exists)
                continue;

            db.ComplianceDocuments.Add(new ComplianceDocument
            {
                Title = title,
                Category = category,
                DocumentType = type,
                ReviewCadence = cadence,
                Status = "Draft",
                NextReviewDate = today.AddMonths(3),
                Tags = tags
            });
        }

        await db.SaveChangesAsync();
    }
}
