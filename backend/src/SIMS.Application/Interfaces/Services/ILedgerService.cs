using SIMS.Domain.Entities.Accounting;

namespace SIMS.Application.Interfaces.Services;

public interface ILedgerService
{
    Task<Guid> PostInvoiceAsync(
        Invoice invoice, int arAccountId, int carrierApAccountId,
        Guid userId, CancellationToken ct = default);
}
