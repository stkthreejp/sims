using Google.Cloud.DocumentAI.V1;
using Google.Apis.Auth.OAuth2;
using Google.Protobuf;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SIMS.Application.Configuration;
using SIMS.Application.DTOs.DocumentAI;
using SIMS.Application.Interfaces.Services;

namespace SIMS.Infrastructure.Services;

public class DocumentAiExtractionService : IDocumentAiExtractionService
{
    private readonly DocumentAiSettings _settings;
    private readonly ILogger<DocumentAiExtractionService> _logger;

    public DocumentAiExtractionService(IConfiguration configuration, ILogger<DocumentAiExtractionService> logger)
    {
        _settings = DocumentAiSettings.FromConfiguration(configuration);
        _logger = logger;
    }

    public async Task<DocumentAiExtractionResult> ProcessAsync(
        byte[] content,
        string mimeType,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        _settings.Validate();

        var client = await CreateClientAsync(cancellationToken);
        var request = new ProcessRequest
        {
            Name = _settings.ProcessorName,
            RawDocument = new RawDocument
            {
                Content = ByteString.CopyFrom(content),
                MimeType = string.IsNullOrWhiteSpace(mimeType) ? "application/pdf" : mimeType
            }
        };

        _logger.LogInformation("Sending {Bytes} bytes to Document AI for {FileName}", content.Length, fileName);

        var response = await client.ProcessDocumentAsync(request, cancellationToken);
        var document = response.Document;
        var fields = ExtractFormFields(document).ToList();

        return DocumentAiExtractionMapper.Summarize(
            _settings.ProcessorName,
            document.Text ?? string.Empty,
            fields,
            _settings.ConfidenceThreshold);
    }

    private Task<DocumentProcessorServiceClient> CreateClientAsync(CancellationToken cancellationToken)
    {
        var builder = new DocumentProcessorServiceClientBuilder
        {
            Endpoint = $"{_settings.Location}-documentai.googleapis.com",
            GoogleCredential = GoogleCredential.FromServiceAccountCredential(
                CredentialFactory.FromJson<ServiceAccountCredential>(_settings.CredentialsJson))
        };

        return builder.BuildAsync(cancellationToken);
    }

    private static IEnumerable<DocumentAiExtractedField> ExtractFormFields(Document document)
    {
        for (var pageIndex = 0; pageIndex < document.Pages.Count; pageIndex++)
        {
            var page = document.Pages[pageIndex];
            foreach (var formField in page.FormFields)
            {
                var name = ReadLayoutText(document.Text, formField.FieldName?.TextAnchor);
                var value = ReadLayoutText(document.Text, formField.FieldValue?.TextAnchor);
                if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(value))
                    continue;

                yield return new DocumentAiExtractedField(
                    name.Trim(),
                    value.Trim(),
                    formField.FieldValue?.Confidence ?? formField.FieldName?.Confidence ?? page.Layout?.Confidence ?? 0,
                    pageIndex + 1);
            }
        }
    }

    private static string ReadLayoutText(string documentText, Document.Types.TextAnchor? textAnchor)
    {
        if (string.IsNullOrEmpty(documentText) || textAnchor == null || textAnchor.TextSegments.Count == 0)
            return string.Empty;

        var parts = new List<string>();
        foreach (var segment in textAnchor.TextSegments)
        {
            var start = (int)segment.StartIndex;
            var end = (int)segment.EndIndex;
            if (start < 0 || end <= start || start >= documentText.Length)
                continue;

            end = Math.Min(end, documentText.Length);
            parts.Add(documentText[start..end]);
        }

        return string.Join(" ", parts).Replace("\n", " ").Trim();
    }
}
