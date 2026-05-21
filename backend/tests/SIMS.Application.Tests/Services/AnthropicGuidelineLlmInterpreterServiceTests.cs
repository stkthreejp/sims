using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SIMS.Domain.Entities;
using SIMS.Infrastructure.Data;
using SIMS.Infrastructure.Services;
using Xunit;

namespace SIMS.Application.Tests.Services;

public class AnthropicGuidelineLlmInterpreterServiceTests
{
    [Fact]
    public async Task InterpretAsync_ParsesValidControlsAndDropsUnsupportedConditionFields()
    {
        await using var db = CreateDb();
        var model = new AiModelRegistry
        {
            Provider = "Anthropic",
            ModelId = "claude-sonnet-default",
            DisplayName = "Claude Sonnet Default",
            Active = true,
            AllowedUseCases = ["ReferralJudgment"]
        };
        db.Add(model);
        db.Add(new AiUseCaseModelSetting
        {
            UseCase = "ReferralJudgment",
            AiModel = model,
            PromptVersion = "smm-underwriter-v1"
        });
        await db.SaveChangesAsync();

        var handler = new FakeHttpMessageHandler("""
            {
              "content": [
                {
                  "type": "text",
                  "text": "{\"controls\":[{\"itemType\":\"DocumentChecklistItem\",\"stage\":\"Submission\",\"severity\":\"Warning\",\"ruleKey\":\"completed-acord-125\",\"label\":\"Completed ACORD 125\",\"description\":\"ACORD 125 is required.\",\"conditionJson\":null,\"isBlocking\":false,\"overrideAllowed\":true,\"overridePermission\":\"underwriting.clearance.override\",\"sourceCitation\":\"Submission requirements\",\"aiConfidence\":0.82,\"sortOrder\":10},{\"itemType\":\"ReferralTrigger\",\"stage\":\"Quote\",\"severity\":\"ReferralRequired\",\"ruleKey\":\"roof-age-over-20\",\"label\":\"Roof age over 20 years\",\"description\":\"Needs a field SIMS does not currently enforce.\",\"conditionJson\":\"{\\\"field\\\":\\\"roofAge\\\",\\\"operator\\\":\\\">\\\",\\\"value\\\":20}\",\"isBlocking\":false,\"overrideAllowed\":true,\"overridePermission\":\"underwriting.clearance.override\",\"sourceCitation\":\"Referral rules\",\"aiConfidence\":0.77,\"sortOrder\":20}]}"
                }
              ]
            }
            """);
        var service = new AnthropicGuidelineLlmInterpreterService(
            new FakeHttpClientFactory(handler),
            Configuration(),
            db,
            NullLogger<AnthropicGuidelineLlmInterpreterService>.Instance);

        var controls = await service.InterpretAsync("Guideline prose");

        Assert.Equal(2, controls.Count);
        Assert.Equal("completed-acord-125", controls[0].RuleKey);
        var unsupported = controls.Single(c => c.RuleKey == "roof-age-over-20");
        Assert.Null(unsupported.ConditionJson);
        Assert.Contains("Unsupported condition field", unsupported.Description);
        Assert.Equal("claude-sonnet-4-20250514", handler.ModelId);
        Assert.Equal("test-key", handler.ApiKey);
    }

    private static IConfiguration Configuration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ANTHROPIC_API_KEY"] = "test-key"
            })
            .Build();

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private sealed class FakeHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler)
        {
            BaseAddress = new Uri("https://api.anthropic.com")
        };
    }

    private sealed class FakeHttpMessageHandler(string responseJson) : HttpMessageHandler
    {
        public string? ApiKey { get; private set; }
        public string? ModelId { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            ApiKey = request.Headers.GetValues("x-api-key").Single();
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            using var document = System.Text.Json.JsonDocument.Parse(body);
            ModelId = document.RootElement.GetProperty("model").GetString();

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };
        }
    }
}
