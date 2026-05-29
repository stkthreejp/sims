using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMS.Application.DTOs.Users;
using SIMS.Application.Interfaces.Services;
using SIMS.Application.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Enums;
using SIMS.Infrastructure.Data;
using Xunit;

namespace SIMS.Application.Tests.Services;

public class UserServiceTests
{
    [Fact]
    public async Task CreateAsync_WhenRoleAssignmentFails_ReturnsFailureAndDoesNotCreateUser()
    {
        await using var fixture = await UserServiceFixture.CreateAsync();

        var result = await fixture.Service.CreateAsync(new UserCreateDto
        {
            UserName = "new.user",
            Email = "new.user@example.com",
            FirstName = "New",
            LastName = "User",
            Password = "P@ssword123!",
            Roles = ["MissingRole"],
        });

        Assert.False(result.IsSuccess);
        Assert.Equal("ROLE_UPDATE_FAILED", result.ErrorCode);
        Assert.Null(await fixture.UserManager.FindByNameAsync("new.user"));
    }

    [Fact]
    public async Task UpdateAsync_WhenRoleAssignmentFails_ReturnsFailureAndLeavesUserAndRolesUnchanged()
    {
        await using var fixture = await UserServiceFixture.CreateAsync();
        await fixture.CreateRoleAsync("ReadOnly");
        var user = await fixture.CreateUserAsync("existing.user", "old@example.com", "ReadOnly");

        var result = await fixture.Service.UpdateAsync(user.Id, new UserUpdateDto
        {
            Email = "new@example.com",
            FirstName = "New",
            LastName = "Name",
            PhoneNumber = "555-0100",
            Status = UserStatus.Active,
            Roles = ["MissingRole"],
        });

        Assert.False(result.IsSuccess);
        Assert.Equal("ROLE_UPDATE_FAILED", result.ErrorCode);

        var saved = await fixture.UserManager.FindByIdAsync(user.Id.ToString());
        Assert.NotNull(saved);
        Assert.Equal("old@example.com", saved!.Email);
        Assert.Equal("Old", saved.FirstName);
        Assert.Equal("User", saved.LastName);
        Assert.Null(saved.PhoneNumber);
        Assert.Equal(UserStatus.Active, saved.Status);

        var roles = await fixture.UserManager.GetRolesAsync(saved);
        Assert.Equal(["ReadOnly"], roles);
    }

    private sealed class UserServiceFixture : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly IServiceScope _scope;

        private UserServiceFixture(ServiceProvider provider, IServiceScope scope)
        {
            _provider = provider;
            _scope = scope;
            Db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            UserManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            RoleManager = scope.ServiceProvider.GetRequiredService<RoleManager<Role>>();
            Service = scope.ServiceProvider.GetRequiredService<IUserService>();
        }

        public ApplicationDbContext Db { get; }
        public UserManager<User> UserManager { get; }
        public RoleManager<Role> RoleManager { get; }
        public IUserService Service { get; }

        public static async Task<UserServiceFixture> CreateAsync()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
            services.AddScoped<DbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
            services.AddIdentity<User, Role>(options =>
                {
                    options.User.RequireUniqueEmail = true;
                    options.Password.RequireDigit = true;
                    options.Password.RequiredLength = 8;
                    options.Password.RequireUppercase = true;
                    options.Password.RequireNonAlphanumeric = true;
                })
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders();
            services.AddScoped<IUserService, UserService>();

            var provider = services.BuildServiceProvider();
            var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Database.EnsureCreatedAsync();

            return new UserServiceFixture(provider, scope);
        }

        public async Task CreateRoleAsync(string roleName)
        {
            var result = await RoleManager.CreateAsync(new Role { Name = roleName });
            Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        public async Task<User> CreateUserAsync(string userName, string email, string roleName)
        {
            var user = new User
            {
                UserName = userName,
                Email = email,
                FirstName = "Old",
                LastName = "User",
                Status = UserStatus.Active,
            };
            var createResult = await UserManager.CreateAsync(user, "P@ssword123!");
            Assert.True(createResult.Succeeded, string.Join(", ", createResult.Errors.Select(e => e.Description)));

            var roleResult = await UserManager.AddToRoleAsync(user, roleName);
            Assert.True(roleResult.Succeeded, string.Join(", ", roleResult.Errors.Select(e => e.Description)));

            return user;
        }

        public async ValueTask DisposeAsync()
        {
            _scope.Dispose();
            await _provider.DisposeAsync();
        }
    }
}
