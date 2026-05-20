using SIMS.Application.DTOs.DocumentAI;

namespace SIMS.Infrastructure.Services;

public static class DocumentAiExtractionMapper
{
    public static DocumentAiExtractionResult Summarize(
        string processorName,
        string text,
        IEnumerable<DocumentAiExtractedField> fields,
        float confidenceThreshold)
    {
        var fieldList = fields.ToList();
        foreach (var field in fieldList)
        {
            field.RequiresReview = field.Confidence < confidenceThreshold;
        }

        return new DocumentAiExtractionResult
        {
            ProcessorName = processorName,
            Text = text,
            Fields = fieldList,
            AverageConfidence = fieldList.Count == 0 ? 0 : fieldList.Average(f => f.Confidence),
            RequiresManualReview = fieldList.Any(f => f.RequiresReview)
        };
    }
}
