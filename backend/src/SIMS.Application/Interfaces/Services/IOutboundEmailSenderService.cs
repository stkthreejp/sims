using SIMS.Application.Common;
using SIMS.Domain.Entities;

namespace SIMS.Application.Interfaces.Services;

public interface IOutboundEmailSenderService
{
    Task<Result<string>> SendAsync(OutboundCommunication communication, CancellationToken cancellationToken = default);
}
