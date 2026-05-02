using SIMS.Application.DTOs.Accounting;

namespace SIMS.Application.Interfaces.Services;

public interface IWireSheetPdfService
{
    /// <summary>Generates the wire instruction PDF bytes for a batch.</summary>
    byte[] Generate(BatchDetailDto batch, string companyName);
}
