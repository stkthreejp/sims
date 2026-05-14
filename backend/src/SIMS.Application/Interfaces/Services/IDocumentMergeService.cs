using SIMS.Application.Common;

namespace SIMS.Application.Interfaces.Services;

public interface IDocumentMergeService
{
    string MergeText(string template, DocumentMergeData data);
    byte[] MergeDocx(byte[] bytes, DocumentMergeData data);
    string FormatValue(object? value, string? format = null);
}
