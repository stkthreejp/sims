using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SIMS.Application.DTOs.DocumentExtraction;
using SIMS.Infrastructure.Services;
using Xunit;

namespace SIMS.Application.Tests.Services;

public class ClaudeSubmissionIntakeAnalyzerTests
{
    private const string ValidResponse = """
        {
          "content": [
            { "type": "text", "text": "{\"boundaries\":[{\"startPage\":1,\"endPage\":2,\"form\":\"Acord126\",\"lineOfBusiness\":\"GeneralLiability\"}],\"quotingLineOfBusiness\":\"GeneralLiability\",\"perLob\":[{\"lineOfBusiness\":\"GeneralLiability\",\"data\":{\"descriptionOfOperations\":\"Logging\"}}],\"confidence\":\"High\"}" }
          ],
          "stop_reason": "end_turn"
        }
        """;

    [Fact]
    public async Task AnalyzeSubmissionAsync_ParsesBoundariesQuotingLineAndPerLob_AndSendsExpectedRequest()
    {
        var handler = new FakeHandler(ValidResponse);
        var analyzer = new ClaudeSubmissionIntakeAnalyzer(new FakeHttpClientFactory(handler), Config(), NullLogger<ClaudeSubmissionIntakeAnalyzer>.Instance);

        var result = await analyzer.AnalyzeSubmissionAsync(Pages(2), "Broker wants GL only.");

        Assert.NotNull(result);
        Assert.Single(result!.Boundaries);
        Assert.Equal("Acord126", result.Boundaries[0].Form);
        Assert.Equal("GeneralLiability", result.Boundaries[0].LineOfBusiness);
        Assert.Equal(1, result.Boundaries[0].StartPage);
        Assert.Equal(2, result.Boundaries[0].EndPage);
        Assert.Equal("GeneralLiability", result.QuotingLineOfBusiness);
        Assert.Single(result.PerLob);
        Assert.Equal("GeneralLiability", result.PerLob[0].LineOfBusiness);
        Assert.Equal("Logging", result.PerLob[0].Data.DescriptionOfOperations);

        // The request carried the auth/version headers, the configured model + inference_geo,
        // and one image block per rendered page.
        Assert.Equal("test-key", handler.ApiKey);
        Assert.Equal("2023-06-01", handler.AnthropicVersion);
        Assert.Equal("claude-opus-4-8", handler.Model);
        Assert.Equal("us", handler.InferenceGeo);
        Assert.Equal(2, handler.ImageBlockCount);
    }

    [Fact]
    public async Task AnalyzeSubmissionAsync_NonSuccessStatus_ReturnsNull()
    {
        var handler = new FakeHandler("{}", HttpStatusCode.InternalServerError);
        var analyzer = new ClaudeSubmissionIntakeAnalyzer(new FakeHttpClientFactory(handler), Config(), NullLogger<ClaudeSubmissionIntakeAnalyzer>.Instance);

        Assert.Null(await analyzer.AnalyzeSubmissionAsync(Pages(1), null));
    }

    [Fact]
    public async Task AnalyzeSubmissionAsync_ResponseWithoutJsonObject_ReturnsNull()
    {
        var handler = new FakeHandler("""
            { "content": [ { "type": "text", "text": "I could not read the document." } ], "stop_reason": "end_turn" }
            """);
        var analyzer = new ClaudeSubmissionIntakeAnalyzer(new FakeHttpClientFactory(handler), Config(), NullLogger<ClaudeSubmissionIntakeAnalyzer>.Instance);

        Assert.Null(await analyzer.AnalyzeSubmissionAsync(Pages(1), null));
    }

    [Fact]
    public async Task AnalyzeSubmissionAsync_ModelRefusal_ReturnsNull()
    {
        var handler = new FakeHandler("""{ "content": [], "stop_reason": "refusal" }""");
        var analyzer = new ClaudeSubmissionIntakeAnalyzer(new FakeHttpClientFactory(handler), Config(), NullLogger<ClaudeSubmissionIntakeAnalyzer>.Instance);

        Assert.Null(await analyzer.AnalyzeSubmissionAsync(Pages(1), null));
    }

    [Fact]
    public async Task AnalyzeSubmissionAsync_MissingApiKey_ThrowsOnlyWhenCalled_NotInConstructor()
    {
        // Constructing with no key must NOT throw (lazy validation — see the inbox-500 fix).
        var analyzer = new ClaudeSubmissionIntakeAnalyzer(
            new FakeHttpClientFactory(new FakeHandler("{}")), Config(withKey: false),
            NullLogger<ClaudeSubmissionIntakeAnalyzer>.Instance);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => analyzer.AnalyzeSubmissionAsync(Pages(1), null));
        Assert.Contains("Anthropic:ApiKey", ex.Message);
    }

    [Fact]
    public async Task AnalyzeSubmissionAsync_NoPages_ReturnsNull()
    {
        var analyzer = new ClaudeSubmissionIntakeAnalyzer(
            new FakeHttpClientFactory(new FakeHandler("{}")), Config(), NullLogger<ClaudeSubmissionIntakeAnalyzer>.Instance);

        Assert.Null(await analyzer.AnalyzeSubmissionAsync([], null));
    }

    private static IReadOnlyList<RenderedPage> Pages(int count) =>
        Enumerable.Range(1, count).Select(i => new RenderedPage(i, [(byte)i])).ToList();

    private static IConfiguration Config(bool withKey = true) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Anthropic:ApiKey"] = withKey ? "test-key" : null,
                ["Anthropic:Model"] = "claude-opus-4-8",
                ["Anthropic:InferenceGeo"] = "us",
            })
            .Build();

    private sealed class FakeHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler) { BaseAddress = new Uri("https://api.anthropic.com") };
    }

    private sealed class FakeHandler(string responseJson, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public string? ApiKey { get; private set; }
        public string? AnthropicVersion { get; private set; }
        public string? Model { get; private set; }
        public string? InferenceGeo { get; private set; }
        public int ImageBlockCount { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            ApiKey = request.Headers.TryGetValues("x-api-key", out var k) ? k.Single() : null;
            AnthropicVersion = request.Headers.TryGetValues("anthropic-version", out var v) ? v.Single() : null;

            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            using var doc = System.Text.Json.JsonDocument.Parse(body);
            var root = doc.RootElement;
            Model = root.GetProperty("model").GetString();
            InferenceGeo = root.TryGetProperty("inference_geo", out var ig) ? ig.GetString() : null;
            ImageBlockCount = root.GetProperty("messages")[0].GetProperty("content")
                .EnumerateArray().Count(e => e.GetProperty("type").GetString() == "image");

            return new HttpResponseMessage(status) { Content = new StringContent(responseJson, Encoding.UTF8, "application/json") };
        }
    }
}
