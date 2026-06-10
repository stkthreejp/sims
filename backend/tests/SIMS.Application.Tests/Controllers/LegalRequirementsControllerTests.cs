using System.Security.Claims;
using Claim = System.Security.Claims.Claim;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SIMS.API.Controllers;
using SIMS.API.Services;
using SIMS.Domain.Entities;
using SIMS.Infrastructure.Data;
using Xunit;

namespace SIMS.Application.Tests.Controllers;

public class LegalRequirementsControllerTests
{
    [Fact]
    public async Task CreateSource_SavesOpenLawApiKeyWithoutReturningSecret()
    {
        await using var db = CreateDbContext();
        var controller = CreateController(db, new FakeOpenLawsClient());
        var input = new LegalTrackedSourceUpsertDto(
            "All",
            "OpenLaw Test",
            "OpenLaw API",
            "https://api.openlaws.us",
            "openlaw-test-key",
            true,
            "Manual",
            null);

        var result = await controller.CreateSource(input);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var dto = Assert.IsType<LegalTrackedSourceDto>(created.Value);
        Assert.True(dto.HasApiKey);
        Assert.Equal("OpenLaw Test", dto.Name);

        var saved = await db.LegalTrackedSources.SingleAsync();
        Assert.Equal("openlaw-test-key", saved.ApiKey);
    }

    [Theory]
    [InlineData("https://evil.example")]
    [InlineData("http://127.0.0.1:8080")]
    [InlineData("http://169.254.169.254/latest")]
    [InlineData("http://[::1]")]
    public async Task CreateSource_RejectsUnsafeOpenLawsBaseUrl(string url)
    {
        await using var db = CreateDbContext();
        var controller = CreateController(db, new FakeOpenLawsClient());
        var input = new LegalTrackedSourceUpsertDto(
            "All",
            "Unsafe OpenLaws",
            "OpenLaws API",
            url,
            "openlaws-test-key",
            true,
            "Manual",
            null);

        var result = await controller.CreateSource(input);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Contains("OpenLaws", badRequest.Value!.ToString());
        Assert.Empty(await db.LegalTrackedSources.ToListAsync());
    }

    [Fact]
    public async Task ScanSource_SearchesOpenLawsForCancellationAndNonrenewal()
    {
        await using var db = CreateDbContext();
        db.LegalRequirementSections.Add(new LegalRequirementSection
        {
            State = "TX",
            LineOfBusiness = "Commercial P&C",
            Action = "Cancellation",
            Category = "NOTICE REQUIREMENTS",
            Topic = "Notice",
            RequirementText = "Existing cancellation requirement.",
            Citations = ["Existing citation"],
            SourceName = "Oden Online Cancellation Chart",
            SourceDocument = "COMMERCIAL INSURANCE - CANCELLATION - P&C",
            SourceCreatedAt = DateTime.UtcNow,
            SortOrder = 1
        });
        var source = new LegalTrackedSource
        {
            State = "TX",
            Name = "OpenLaws TX",
            SourceType = "OpenLaw API",
            Url = "https://api.openlaws.us",
            ApiKey = "openlaws-test-key",
            IsEnabled = true,
            ScanCadence = "Manual",
            LastStatus = "NotChecked"
        };
        db.LegalTrackedSources.Add(source);
        await db.SaveChangesAsync();

        var client = new FakeOpenLawsClient(new[]
        {
            new OpenLawsSearchResult(
                "TX",
                "TX-INS",
                "section_551_105",
                "Section 551.105: Commercial cancellation notice",
                "551.105",
                "https://openlaws.test/tx/551_105",
                "Cancellation notice text from OpenLaws.")
        });
        var controller = CreateController(db, client);

        var result = await controller.ScanSource(source.Id);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var run = Assert.IsType<LegalSourceScanRunDto>(ok.Value);
        Assert.Equal("Completed", run.Status);
        Assert.Equal(1, run.ResultsFound);
        Assert.Equal(1, run.PossibleChanges);
        Assert.Contains(client.Requests, request => request.Query.Contains("cancellation", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(client.Requests, request => request.Query.Contains("nonrenewal", StringComparison.OrdinalIgnoreCase));
        Assert.All(client.Requests, request =>
        {
            Assert.Equal("TX", request.Jurisdiction);
            Assert.Equal("openlaws-test-key", request.ApiKey);
        });

        var scanResult = await db.LegalSourceScanResults.SingleAsync();
        Assert.Equal("TX", scanResult.State);
        Assert.Equal("NOTICE REQUIREMENTS", scanResult.Category);
        Assert.Equal("Section 551.105: Commercial cancellation notice", scanResult.Topic);
        Assert.Equal("TX-INS 551.105", scanResult.SourceCitation);
        Assert.Equal("Cancellation notice text from OpenLaws.", scanResult.SourceText);
        Assert.Equal("Pending", scanResult.ReviewStatus);
    }

    [Fact]
    public async Task ScanSource_ConvertsStateNameToOpenLawsJurisdictionKey()
    {
        await using var db = CreateDbContext();
        var source = new LegalTrackedSource
        {
            State = "Texas",
            Name = "OpenLaws Texas",
            SourceType = "OpenLaw API",
            Url = "https://api.openlaws.us",
            ApiKey = "openlaws-test-key",
            IsEnabled = true,
            ScanCadence = "Manual",
            LastStatus = "NotChecked"
        };
        db.LegalTrackedSources.Add(source);
        await db.SaveChangesAsync();

        var client = new FakeOpenLawsClient(new[]
        {
            new OpenLawsSearchResult(
                "TX",
                "TX-INS",
                "section_551_105",
                "Section 551.105: Commercial cancellation notice",
                "551.105",
                "https://openlaws.test/tx/551_105",
                "Cancellation notice text from OpenLaws.")
        });
        var controller = CreateController(db, client);

        await controller.ScanSource(source.Id);

        Assert.All(client.Requests, request => Assert.Equal("TX", request.Jurisdiction));

        var scanResult = await db.LegalSourceScanResults.SingleAsync();
        Assert.Equal("Texas", scanResult.State);
    }

    [Fact]
    public async Task OpenLawsClient_MapsSnakeCaseSearchResults()
    {
        var handler = new CaptureHandler("""
            [
              {
                "jurisdiction_key": "TX",
                "law_key": "TX-INS",
                "path": "title_28.section_551_105",
                "display_name": "Section 551.105: Notice",
                "identifier": "551.105",
                "openlaws_web_url": "https://openlaws.test/tx/551_105",
                "plaintext_content": "Notice content"
              }
            ]
            """);
        var client = new OpenLawsClient(new FakeHttpClientFactory(handler));

        var results = await client.SearchAsync(
            new OpenLawsSearchRequest("https://api.openlaws.us", "secret-key", "TX", "commercial insurance cancellation notice", 5),
            CancellationToken.None);

        var result = Assert.Single(results);
        Assert.Equal("TX", result.Jurisdiction);
        Assert.Equal("TX-INS", result.LawKey);
        Assert.Equal("title_28.section_551_105", result.Path);
        Assert.Equal("Section 551.105: Notice", result.DisplayName);
        Assert.Equal("551.105", result.Identifier);
        Assert.Equal("https://openlaws.test/tx/551_105", result.WebUrl);
        Assert.Equal("Notice content", result.Text);
        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Equal("secret-key", handler.AuthorizationParameter);
        Assert.Contains("/api/v1/jurisdictions/TX/laws/search", handler.RequestUri);
        Assert.Contains("type=phrase", handler.RequestUri);
        Assert.Contains("with_federal=true", handler.RequestUri);
    }

    [Fact]
    public async Task OpenLawsClient_TreatsNoMatchingDivisionsAsEmptyResults()
    {
        var handler = new CaptureHandler(
            """{"status":404,"title":"No matching Divisions for that search."}""",
            System.Net.HttpStatusCode.NotFound);
        var client = new OpenLawsClient(new FakeHttpClientFactory(handler));

        var results = await client.SearchAsync(
            new OpenLawsSearchRequest("https://api.openlaws.us", "secret-key", "TX", "unlikely phrase", 5),
            CancellationToken.None);

        Assert.Empty(results);
    }

    [Fact]
    public async Task OpenLawsClient_RejectsUnsafeBaseUrlBeforeRequest()
    {
        var handler = new CaptureHandler("[]");
        var client = new OpenLawsClient(new FakeHttpClientFactory(handler));

        await Assert.ThrowsAsync<OpenLawsException>(() =>
            client.SearchAsync(
                new OpenLawsSearchRequest("http://127.0.0.1:8080", "secret-key", "TX", "commercial insurance cancellation notice", 5),
                CancellationToken.None));

        Assert.Equal(string.Empty, handler.RequestUri);
    }

    [Fact]
    public async Task ScanSource_ReturnsFailedRunWhenOpenLawsFails()
    {
        await using var db = CreateDbContext();
        var source = new LegalTrackedSource
        {
            State = "TX",
            Name = "OpenLaws TX",
            SourceType = "OpenLaw API",
            Url = "https://api.openlaws.us",
            ApiKey = "openlaws-test-key",
            IsEnabled = true,
            ScanCadence = "Manual",
            LastStatus = "NotChecked"
        };
        db.LegalTrackedSources.Add(source);
        await db.SaveChangesAsync();

        var controller = CreateController(db, new FailingOpenLawsClient());

        var result = await controller.ScanSource(source.Id);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var run = Assert.IsType<LegalSourceScanRunDto>(ok.Value);
        Assert.Equal("Failed", run.Status);
        Assert.Equal("OpenLaws returned 401 Unauthorized: Invalid token", run.ErrorMessage);

        var savedSource = await db.LegalTrackedSources.SingleAsync();
        Assert.Equal("Failed", savedSource.LastStatus);
        Assert.Equal("OpenLaws returned 401 Unauthorized: Invalid token", savedSource.LastErrorMessage);
    }

    [Fact]
    public async Task ScanSource_RejectsStoredUnsafeOpenLawsBaseUrlBeforeClientCall()
    {
        await using var db = CreateDbContext();
        var source = new LegalTrackedSource
        {
            State = "TX",
            Name = "Unsafe OpenLaws TX",
            SourceType = "OpenLaw API",
            Url = "http://127.0.0.1:8080",
            ApiKey = "openlaws-test-key",
            IsEnabled = true,
            ScanCadence = "Manual",
            LastStatus = "NotChecked"
        };
        db.LegalTrackedSources.Add(source);
        await db.SaveChangesAsync();

        var client = new FakeOpenLawsClient();
        var controller = CreateController(db, client);

        var result = await controller.ScanSource(source.Id);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Empty(client.Requests);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private static LegalRequirementsController CreateController(ApplicationDbContext db, IOpenLawsClient client)
    {
        return new LegalRequirementsController(db, client, NullLogger<LegalRequirementsController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([
                        new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                        new Claim(ClaimTypes.Name, "Test User")
                    ], "Test"))
                },
                ActionDescriptor = new ControllerActionDescriptor()
            }
        };
    }

    private sealed class FakeOpenLawsClient(IReadOnlyList<OpenLawsSearchResult>? results = null) : IOpenLawsClient
    {
        public List<OpenLawsSearchRequest> Requests { get; } = [];

        public Task<IReadOnlyList<OpenLawsSearchResult>> SearchAsync(OpenLawsSearchRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(results ?? []);
        }
    }

    private sealed class FailingOpenLawsClient : IOpenLawsClient
    {
        public Task<IReadOnlyList<OpenLawsSearchResult>> SearchAsync(OpenLawsSearchRequest request, CancellationToken cancellationToken)
        {
            throw new OpenLawsException("OpenLaws returned 401 Unauthorized: Invalid token");
        }
    }

    private sealed class FakeHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler);
    }

    private sealed class CaptureHandler(string responseBody, System.Net.HttpStatusCode statusCode = System.Net.HttpStatusCode.OK) : HttpMessageHandler
    {
        public string RequestUri { get; private set; } = string.Empty;
        public string? AuthorizationScheme { get; private set; }
        public string? AuthorizationParameter { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri?.ToString() ?? string.Empty;
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            AuthorizationParameter = request.Headers.Authorization?.Parameter;

            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            });
        }
    }
}
