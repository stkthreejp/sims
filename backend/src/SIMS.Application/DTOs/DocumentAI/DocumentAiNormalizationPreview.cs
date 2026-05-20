using SIMS.Application.DTOs.Gemini;
using SIMS.Application.DTOs.Submissions;

namespace SIMS.Application.DTOs.DocumentAI;

public class DocumentAiNormalizationPreview
{
    public GeminiExtractionResult SubmissionData { get; set; } = new();
    public List<SubmissionLossYearCreateDto> LossYears { get; set; } = [];
    public List<DocumentAiExtractedField> FieldsRequiringReview { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
}
