namespace SIMS.Application.DTOs.DocumentAI;

public class DocumentAiExtractionResult
{
    public string ProcessorName { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public float AverageConfidence { get; set; }
    public bool RequiresManualReview { get; set; }
    public List<DocumentAiExtractedField> Fields { get; set; } = [];
}

public class DocumentAiExtractedField
{
    public DocumentAiExtractedField(string name, string value, float confidence, int pageNumber)
    {
        Name = name;
        Value = value;
        Confidence = confidence;
        PageNumber = pageNumber;
    }

    public string Name { get; set; }
    public string Value { get; set; }
    public float Confidence { get; set; }
    public int PageNumber { get; set; }
    public bool RequiresReview { get; set; }
}
