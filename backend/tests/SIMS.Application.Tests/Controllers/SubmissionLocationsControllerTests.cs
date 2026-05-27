using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIMS.API.Controllers;
using SIMS.Application.DTOs.Submissions;
using SIMS.Domain.Entities;
using SIMS.Infrastructure.Data;
using Xunit;

namespace SIMS.Application.Tests.Controllers;

public class SubmissionLocationsControllerTests
{
    [Fact]
    public async Task Create_MarksFirstLocationPrimaryAndReturnsFullRiskAddress()
    {
        await using var db = CreateDb();
        var submissionId = await SeedSubmissionAsync(db);
        var controller = new SubmissionLocationsController(db);

        var result = await controller.Create(submissionId, new SubmissionLocationCreateDto
        {
            LocationNumber = 1,
            Address = " 100 Main St ",
            City = " Jackson ",
            State = " ms ",
            County = " Hinds ",
            ZipCode = " 39000 ",
            Country = " usa ",
            IsPrimary = false
        });

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var dto = Assert.IsType<SubmissionLocationDto>(created.Value);
        Assert.Equal("100 Main St", dto.Address);
        Assert.Equal("Jackson", dto.City);
        Assert.Equal("MS", dto.State);
        Assert.Equal("Hinds", dto.County);
        Assert.Equal("39000", dto.ZipCode);
        Assert.Equal("USA", dto.Country);
        Assert.True(dto.IsPrimary);
    }

    [Fact]
    public async Task Update_WhenMarkedPrimary_ClearsPreviousPrimaryLocation()
    {
        await using var db = CreateDb();
        var submissionId = await SeedSubmissionAsync(db);
        var first = new SubmissionLocation
        {
            SubmissionId = submissionId,
            LocationNumber = 1,
            Address = "100 Main St",
            IsPrimary = true
        };
        var second = new SubmissionLocation
        {
            SubmissionId = submissionId,
            LocationNumber = 2,
            Address = "200 Yard Rd",
            IsPrimary = false
        };
        db.SubmissionLocations.AddRange(first, second);
        await db.SaveChangesAsync();
        var controller = new SubmissionLocationsController(db);

        var result = await controller.Update(submissionId, second.Id, new SubmissionLocationUpdateDto
        {
            LocationNumber = 2,
            Address = "200 Yard Rd",
            City = "Tupelo",
            State = "MS",
            County = "Lee",
            ZipCode = "38801",
            Country = "USA",
            IsPrimary = true
        });

        Assert.IsType<OkObjectResult>(result);
        Assert.False((await db.SubmissionLocations.FindAsync(first.Id))!.IsPrimary);
        Assert.True((await db.SubmissionLocations.FindAsync(second.Id))!.IsPrimary);
    }

    [Fact]
    public async Task Delete_WhenPrimary_RemakesNextLocationPrimary()
    {
        await using var db = CreateDb();
        var submissionId = await SeedSubmissionAsync(db);
        var first = new SubmissionLocation
        {
            SubmissionId = submissionId,
            LocationNumber = 1,
            Address = "100 Main St",
            IsPrimary = true
        };
        var second = new SubmissionLocation
        {
            SubmissionId = submissionId,
            LocationNumber = 2,
            Address = "200 Yard Rd",
            IsPrimary = false
        };
        db.SubmissionLocations.AddRange(first, second);
        await db.SaveChangesAsync();
        var controller = new SubmissionLocationsController(db);

        var result = await controller.Delete(submissionId, first.Id);

        Assert.IsType<NoContentResult>(result);
        Assert.True((await db.SubmissionLocations.IgnoreQueryFilters().SingleAsync(l => l.Id == first.Id)).IsDeleted);
        Assert.True((await db.SubmissionLocations.FindAsync(second.Id))!.IsPrimary);
    }

    private static async Task<Guid> SeedSubmissionAsync(ApplicationDbContext db)
    {
        var submission = new Submission
        {
            SubmissionNumber = "SUB-TEST",
            InsuredId = Guid.NewGuid(),
            UnderwriterId = Guid.NewGuid(),
            CreatedById = Guid.NewGuid()
        };
        db.Submissions.Add(submission);
        await db.SaveChangesAsync();
        return submission.Id;
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }
}
