using IMS.Application.Interfaces.Services;
using IMS.Application.Services;
using IMS.Domain.Entities;
using IMS.Infrastructure.Data;
using IMS.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IMS.Infrastructure.Extensions;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // EF Core + PostgreSQL
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"),
                npgsql => npgsql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)));

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

        // Application services
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAgentService, AgentService>();
        services.AddScoped<ICarrierService, CarrierService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IInsuredService, InsuredService>();
        services.AddScoped<ISubmissionService, SubmissionService>();
        services.AddScoped<IQuoteService, QuoteService>();
        services.AddScoped<INoteService, NoteService>();
        services.AddSingleton<IBlobStorageService, AzureBlobStorageService>();
        services.AddScoped<IAttachmentService, AttachmentService>();
        services.AddScoped<IDocumentTemplateService, DocumentTemplateService>();
        services.AddScoped<IDocumentGenerationService, DocumentGenerationService>();

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
                "policies.attachments.upload", "policies.attachments.delete"
            }),
            ["CSR"] = ("Customer service", new[] {
                "insureds.view", "insureds.create", "insureds.edit",
                "policies.view", "policies.notes.create", "policies.notes.edit",
                "policies.attachments.upload"
            }),
            ["ReadOnly"] = ("Read only access", new[] {
                "insureds.view", "policies.view"
            }),
        };

        foreach (var (roleName, (description, perms)) in roleDefinitions)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var role = new Role { Name = roleName, Description = description, IsSystemRole = true };
                await roleManager.CreateAsync(role);

                var createdRole = await roleManager.FindByNameAsync(roleName);
                if (createdRole != null)
                {
                    var permEntities = db.Permissions.Where(p => perms.Contains(p.Name)).ToList();
                    foreach (var perm in permEntities)
                        db.RolePermissions.Add(new RolePermission { RoleId = createdRole.Id, PermissionId = perm.Id });
                }
            }
        }
        await db.SaveChangesAsync();

        // Seed admin user
        if (await userManager.FindByNameAsync("admin") == null)
        {
            var admin = new User
            {
                UserName = "admin",
                Email = "admin@ims.local",
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
